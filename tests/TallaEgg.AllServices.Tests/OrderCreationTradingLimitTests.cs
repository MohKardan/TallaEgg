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
using TallaEgg.Core.ErrorHandling;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A symbol's size limits apply to every order, whichever path created it (issues #277, #278).
///
/// <para>
/// <b>What was wrong:</b> <c>ValidateTradingLimits</c> ran only inside <c>POST /api/orders</c>.
/// Every symbol trades in dealer mode, so customers buy through <c>POST /api/quotes/accept</c>,
/// which never called it — a 0.05 g accept settled against a 0.1 g minimum. And on the one path
/// that did check, the refusal was caught and rethrown as «خطا در ایجاد سفارش», so the caller never
/// learned which limit it had broken.
/// </para>
///
/// <para>
/// The check now sits where every order is created, so these tests drive the two public paths and
/// assert on what reaches the caller and on what is left in the database afterwards.
/// </para>
/// </summary>
public class OrderCreationTradingLimitTests : IDisposable
{
    private const string Gold = CurrenciesConstant.MAUA_IRT;
    private const string Coin = CurrenciesConstant.SEKE_BAHAR_IRT;

    // Realistic quotes, so a limit that happens to depend on price is tested at the price it will
    // actually meet: gold per gram, and the Bahar coin at the 2026-09-10 level.
    private const decimal GoldBuyPrice = 17_000_000m;
    private const decimal GoldSellPrice = 17_200_000m;
    private const decimal CoinBuyPrice = 97_500_000m;
    private const decimal CoinSellPrice = 97_900_000m;

    private readonly SqliteConnection _connection;
    private readonly Guid _customer = Guid.NewGuid();
    private readonly Guid _dealer = Guid.NewGuid();
    private readonly RecordingWallet _wallet = new();

    public OrderCreationTradingLimitTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    // ── the quote-fill path, which is the one customers use ─────────────────────

    [Fact]
    public async Task AcceptQuoteAsync_QuantityBelowMinQuantity_IsRefusedWithTheLimitMessage()
    {
        await PublishQuoteAsync(Gold, GoldBuyPrice, GoldSellPrice);
        var tooSmall = Pair(Gold).MinQuantity / 2m;

        var (success, message, trade) =
            await BuildQuoteFillService().AcceptQuoteAsync(_customer, Gold, OrderSide.Buy, tooSmall);

        Assert.False(success);
        Assert.Null(trade);
        Assert.Contains("کمتر از", message);
    }

    [Fact]
    public async Task AcceptQuoteAsync_QuantityAboveMaxQuantity_IsRefusedWithTheLimitMessage()
    {
        await PublishQuoteAsync(Gold, GoldBuyPrice, GoldSellPrice);
        var tooLarge = Pair(Gold).MaxQuantity + 1m;

        var (success, message, _) =
            await BuildQuoteFillService().AcceptQuoteAsync(_customer, Gold, OrderSide.Buy, tooLarge);

        Assert.False(success);
        Assert.Contains("بیشتر از", message);
    }

    /// <summary>
    /// The limit that matters most: a quantity at the minimum can still be worth less than the
    /// cost of settling it. One hundredth of a coin clears the coin's quantity floor and falls
    /// short of its notional floor.
    /// </summary>
    [Fact]
    public async Task AcceptQuoteAsync_NotionalBelowMinNotional_IsRefusedWithTheLimitMessage()
    {
        await PublishQuoteAsync(Coin, CoinBuyPrice, CoinSellPrice);
        var pair = Pair(Coin);
        var quantity = pair.MinQuantity;

        // Without this the test could pass by tripping the quantity limit instead.
        Assert.True(quantity * CoinSellPrice < pair.MinNotional,
            "Precondition: this quantity must clear MinQuantity and fall short of MinNotional.");

        var (success, message, _) =
            await BuildQuoteFillService().AcceptQuoteAsync(_customer, Coin, OrderSide.Buy, quantity);

        Assert.False(success);
        Assert.Contains("ارزش سفارش", message);
    }

    /// <summary>
    /// A refusal has to happen before anything is written. Refusing after the order was saved or
    /// the collateral locked would trade one defect for a worse one: a Pending row, or money held
    /// against nothing.
    /// </summary>
    [Fact]
    public async Task AcceptQuoteAsync_RefusedByATradingLimit_CreatesNoOrderAndLocksNothing()
    {
        await PublishQuoteAsync(Gold, GoldBuyPrice, GoldSellPrice);

        await BuildQuoteFillService().AcceptQuoteAsync(_customer, Gold, OrderSide.Buy, Pair(Gold).MinQuantity / 2m);

        Assert.Empty(await OrdersOfAsync(_customer));
        Assert.Empty(await OrdersOfAsync(_dealer));
        Assert.Empty(_wallet.Locks);
    }

    /// <summary>
    /// Exactly at the minimum is allowed, for both sides. Guards against the check refusing more
    /// than it should — the dealer's order passes through the same gate as the customer's.
    /// </summary>
    /// <remarks>Green before the fix too; it holds the boundary once the check is live.</remarks>
    [Fact]
    public async Task AcceptQuoteAsync_QuantityExactlyAtMinQuantity_Succeeds()
    {
        await PublishQuoteAsync(Gold, GoldBuyPrice, GoldSellPrice);

        var (success, message, trade) =
            await BuildQuoteFillService().AcceptQuoteAsync(_customer, Gold, OrderSide.Buy, Pair(Gold).MinQuantity);

        Assert.True(success, message);
        Assert.NotNull(trade);
        Assert.Single(await OrdersOfAsync(_customer));
        Assert.Single(await OrdersOfAsync(_dealer));
    }

    /// <summary>
    /// The dealer's order is created after the customer's has already been saved and locked. If a
    /// refusal on the dealer's side escaped instead of being handled, the customer's collateral
    /// would stay locked with no trade behind it. This pins the release.
    /// </summary>
    /// <remarks>Green before the fix too; it guards the refusal plumbing this change touches.</remarks>
    [Fact]
    public async Task AcceptQuoteAsync_DealersLockFails_CancelsTheCustomersOrder()
    {
        await PublishQuoteAsync(Gold, GoldBuyPrice, GoldSellPrice);
        _wallet.FailLockFor = _dealer;

        var (success, message, _) =
            await BuildQuoteFillService().AcceptQuoteAsync(_customer, Gold, OrderSide.Buy, 1m);

        Assert.False(success);
        Assert.Equal("در حال حاضر امکان انجام این معامله نیست.", message);

        var customerOrder = Assert.Single(await OrdersOfAsync(_customer));
        Assert.Equal(OrderStatus.Cancelled, customerOrder.Status);
    }

    // ── POST /api/orders ────────────────────────────────────────────────────────

    /// <summary>
    /// The order path always checked the limit, then hid it: the refusal was rethrown as the
    /// generic «خطا در ایجاد سفارش», so a caller saw that something failed and never what.
    /// </summary>
    [Fact]
    public async Task CreateOrderAsync_QuantityBelowMinQuantity_ThrowsTheLimitMessageNotTheGenericOne()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            BuildOrderService().CreateOrderAsync(new OrderDto
            {
                Asset = Gold,
                Amount = Pair(Gold).MinQuantity / 2m,
                Price = GoldSellPrice,
                UserId = _customer,
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                TradingType = TradingType.Spot
            }));

        Assert.Contains("کمتر از", ex.Message);
        Assert.DoesNotContain("خطا در ایجاد سفارش", ex.Message);
    }

    // ── harness ─────────────────────────────────────────────────────────────────

    private static TradingPairInfo Pair(string symbol) => CurrenciesConstant.GetTradingPairInfo(symbol)!;

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    private async Task PublishQuoteAsync(string symbol, decimal buyPrice, decimal sellPrice)
    {
        await using var db = NewContext();
        db.Quotes.Add(Quote.Publish(symbol, buyPrice, sellPrice, _dealer));
        await db.SaveChangesAsync();
    }

    private async Task<List<Order>> OrdersOfAsync(Guid userId)
    {
        await using var db = NewContext();
        return await db.Orders.Where(o => o.UserId == userId).ToListAsync();
    }

    /// <summary>
    /// Approves every balance check, records every lock, and fails the lock for one chosen user.
    /// </summary>
    private sealed class RecordingWallet : StubWalletApiClient
    {
        public List<(Guid UserId, string Asset, decimal Amount)> Locks { get; } = new();
        public Guid? FailLockFor { get; set; }

        public override Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
            ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price) =>
            Task.FromResult((true, "ok", true, true));

        public override Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
            Guid userId, string asset, decimal amount)
        {
            if (userId == FailLockFor)
                return Task.FromResult((false, "کیف پول پیدا نشد", (WalletDTO?)null));

            Locks.Add((userId, asset, amount));
            return Task.FromResult((true, "locked", (WalletDTO?)new WalletDTO()));
        }

        public override Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount) =>
            Task.FromResult((true, "unlocked"));
    }

    private IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Matching:MarketModes:" + Gold] = "Dealer",
            ["Matching:MarketModes:" + Coin] = "Dealer",
            // Required by the UsersApiClient constructor (issue #205); the canned handler answers
            // whatever is asked, so the host is only a label.
            ["UsersApiUrl"] = "http://users.test/api"
        })
        .Build();

    private OrderService BuildOrderService(OrdersDbContext? context = null)
    {
        context ??= NewContext();
        var configuration = Configuration();

        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);

        // 404 rather than a user: the caller is not an administrator, so the customer's path runs.
        var usersApiClient = new UsersApiClient(
            new HttpClient(new NotFoundHandler()), configuration, NullLogger<UsersApiClient>.Instance);

        return new OrderService(
            orderRepository, _wallet, new NoOpMatchingEngine(),
            NullLogger<OrderService>.Instance,
            usersApiClient,
            new OrderCollateralReconciler(orderRepository, new TradeRepository(context), _wallet,
                NullLogger<OrderCollateralReconciler>.Instance),
            new QuoteRepository(context, NullLogger<QuoteRepository>.Instance),
            new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance));
    }

    private QuoteFillService BuildQuoteFillService()
    {
        var context = NewContext();
        var configuration = Configuration();

        return new QuoteFillService(
            new QuoteRepository(context, NullLogger<QuoteRepository>.Instance),
            BuildOrderService(context),
            new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance),
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
