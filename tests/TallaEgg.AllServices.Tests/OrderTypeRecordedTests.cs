using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That an order records how it was placed (issue #250).
///
/// <para>
/// <b>The model these tests encode</b>, confirmed with the product owner on 2026-09-08. The shop
/// publishes a quote; a customer fills it. Both sides of that fill are orders, and the two are not
/// the same kind of order: the dealer <i>named</i> the price, so their side is a
/// <see cref="OrderType.Limit"/> order, while the customer <i>took</i> the price that was there, so
/// their side is a <see cref="OrderType.Market"/> order. One trade, one price, two order types.
/// </para>
///
/// <para>
/// Ordinary customers cannot place a limit order today — the dealer is the only participant who
/// names a price — and that is what "we have no peer-to-peer trading yet" means in this codebase.
/// It is why <see cref="OrderDto.Type"/> stays on the request rather than being removed: it becomes
/// the customer's choice when peer-to-peer trading opens.
/// </para>
///
/// <para>
/// <b>What was wrong:</b> <c>Order.Type</c> was never assigned by any factory, so every order in
/// the database carried the enum default — <see cref="OrderType.Market"/>, value <c>0</c> —
/// whatever it actually was. That made the column accidentally right for the customer's side of
/// every fill and wrong for the dealer's, which is why only half of these tests could go red.
/// </para>
/// </summary>
public class OrderTypeRecordedTests : IDisposable
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal BuyPrice = 17_000_000m;
    private const decimal SellPrice = 17_200_000m;
    private const decimal Quantity = 1m;

    private readonly SqliteConnection _connection;
    private readonly Guid _customer = Guid.NewGuid();
    private readonly Guid _dealer = Guid.NewGuid();

    public OrderTypeRecordedTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    // ── the entity records what it is told ──────────────────────────────────────

    /// <summary>
    /// The factory every order in production goes through. It took the side and the trading type
    /// and silently left the order type at the enum default, which is the whole of issue #250.
    /// </summary>
    [Theory]
    [InlineData(OrderType.Market)]
    [InlineData(OrderType.Limit)]
    [InlineData(OrderType.StopLimit)]
    [InlineData(OrderType.Oco)]
    public void CreateMakerOrder_GivenAnOrderType_RecordsThatType(OrderType orderType)
    {
        var order = Order.CreateMakerOrder(
            Symbol, Quantity, BuyPrice, _customer, OrderSide.Buy, orderType, TradingType.Spot);

        Assert.Equal(orderType, order.Type);
    }

    // ── the two sides of a quote fill ───────────────────────────────────────────

    /// <summary>
    /// The customer took a price somebody else published, so their order is a market order.
    /// </summary>
    /// <remarks>
    /// This one cannot go red on the unfixed code — <see cref="OrderType.Market"/> is the enum
    /// default, so the column already said this by accident. It is here to hold the value once it
    /// is deliberate, and it is paired with
    /// <see cref="TheDealersSideOfAQuoteFill_IsRecordedAsALimitOrder"/>, which can.
    /// </remarks>
    [Fact]
    public async Task TheCustomersSideOfAQuoteFill_IsRecordedAsAMarketOrder()
    {
        await PublishQuoteAsync();

        await BuildQuoteFillService().AcceptQuoteAsync(_customer, Symbol, OrderSide.Buy, Quantity);

        var customerOrder = Assert.Single(await OrdersOfAsync(_customer));
        Assert.Equal(OrderType.Market, customerOrder.Type);
    }

    /// <summary>
    /// The dealer published the price, so their side is a limit order. Before the fix this said
    /// <c>Market</c>, like every other row in the table.
    /// </summary>
    [Fact]
    public async Task TheDealersSideOfAQuoteFill_IsRecordedAsALimitOrder()
    {
        await PublishQuoteAsync();

        await BuildQuoteFillService().AcceptQuoteAsync(_customer, Symbol, OrderSide.Buy, Quantity);

        var dealerOrder = Assert.Single(await OrdersOfAsync(_dealer));
        Assert.Equal(OrderType.Limit, dealerOrder.Type);
    }

    /// <summary>
    /// The sharpest statement of the model: one fill, one price, two different order types. Both
    /// sides used to carry the same value, which is what made the column useless.
    /// </summary>
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public async Task TheTwoSidesOfOneFill_DoNotShareAnOrderType(OrderSide customerSide)
    {
        await PublishQuoteAsync();

        await BuildQuoteFillService().AcceptQuoteAsync(_customer, Symbol, customerSide, Quantity);

        var customerOrder = Assert.Single(await OrdersOfAsync(_customer));
        var dealerOrder = Assert.Single(await OrdersOfAsync(_dealer));

        Assert.Equal(OrderType.Market, customerOrder.Type);
        Assert.Equal(OrderType.Limit, dealerOrder.Type);
    }

    // ── POST /api/orders keeps the type the caller sent ─────────────────────────

    /// <summary>
    /// The request's <c>type</c> was read into a log line and thrown away. It is now what the
    /// order records, which is the only reading under which the field means anything.
    /// </summary>
    /// <remarks>
    /// <c>StopLimit</c> and <c>Oco</c> are deliberately included. The endpoint cannot honour them —
    /// it builds a resting order whatever it is sent — and refusing them would be a behaviour
    /// change this issue does not make. Recording what was asked for is what lets that refusal be
    /// written later against evidence rather than against a column of zeroes.
    /// </remarks>
    [Theory]
    [InlineData(OrderType.Market)]
    [InlineData(OrderType.Limit)]
    [InlineData(OrderType.StopLimit)]
    [InlineData(OrderType.Oco)]
    public async Task PostOrders_RecordsTheTypeTheCallerSent(OrderType requested)
    {
        await BuildOrderService().CreateOrderAsync(new OrderDto
        {
            Asset = Symbol,
            Amount = Quantity,
            Price = BuyPrice,
            UserId = _customer,
            Side = OrderSide.Buy,
            Type = requested,
            TradingType = TradingType.Spot
        });

        var order = Assert.Single(await OrdersOfAsync(_customer));
        Assert.Equal(requested, order.Type);
    }

    // ── harness ─────────────────────────────────────────────────────────────────

    private async Task PublishQuoteAsync()
    {
        await using var db = NewContext();
        db.Quotes.Add(Quote.Publish(Symbol, BuyPrice, SellPrice, _dealer));
        await db.SaveChangesAsync();
    }

    private async Task<List<Order>> OrdersOfAsync(Guid userId)
    {
        await using var db = NewContext();
        return await db.Orders.Where(o => o.UserId == userId).ToListAsync();
    }

    /// <summary>Approves every check and records nothing; these tests are about one column.</summary>
    private sealed class PermissiveWallet : StubWalletApiClient
    {
        public override Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
            ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price) =>
            Task.FromResult((true, "ok", true, true));

        public override Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
            Guid userId, string asset, decimal amount) =>
            Task.FromResult((true, "locked", (WalletDTO?)new WalletDTO()));

        public override Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount) =>
            Task.FromResult((true, "unlocked"));
    }

    private readonly PermissiveWallet _wallet = new();

    private OrderService BuildOrderService()
    {
        var context = NewContext();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:MarketModes:" + Symbol] = "Dealer",
                // The constructor requires this key (issue #205). The canned handler answers
                // whatever is asked, so the host is only ever a label.
                ["UsersApiUrl"] = "http://users.test/api"
            })
            .Build();

        var marketMode = new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);
        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);
        var tradeRepository = new TradeRepository(context);

        // 404 rather than a user: GetUserByIdAsync returns null, so the caller is not an
        // administrator and the ordinary balance path runs. Which path it takes does not matter
        // here, but taking the customer's is the honest default.
        var usersApiClient = new UsersApiClient(
            new HttpClient(new NotFoundHandler()), configuration, NullLogger<UsersApiClient>.Instance);

        return new OrderService(
            orderRepository, _wallet, new NoOpMatchingEngine(),
            NullLogger<OrderService>.Instance,
            usersApiClient,
            new OrderCollateralReconciler(orderRepository, tradeRepository, _wallet,
                NullLogger<OrderCollateralReconciler>.Instance),
            quoteRepository, marketMode);
    }

    private QuoteFillService BuildQuoteFillService()
    {
        var context = NewContext();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:MarketModes:" + Symbol] = "Dealer"
            })
            .Build();

        var marketMode = new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);
        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);
        var tradeRepository = new TradeRepository(context);

        var orderService = new OrderService(
            orderRepository, _wallet, new NoOpMatchingEngine(),
            NullLogger<OrderService>.Instance,
            UsersApiClient: null!,
            new OrderCollateralReconciler(orderRepository, tradeRepository, _wallet,
                NullLogger<OrderCollateralReconciler>.Instance),
            quoteRepository, marketMode);

        return new QuoteFillService(
            quoteRepository, orderService, marketMode,
            new OrderMatchingRepository(context, NullLogger<OrderMatchingRepository>.Instance),
            _wallet, NullLogger<QuoteFillService>.Instance);
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"success\":false}", Encoding.UTF8, "application/json")
            });
    }

    private sealed class NoOpMatchingEngine : IMatchingEngine
    {
        public Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
