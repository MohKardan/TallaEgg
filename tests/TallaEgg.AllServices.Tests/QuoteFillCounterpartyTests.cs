using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The other side of a quote fill is whoever published the quote.
///
/// <para>
/// It used to be a user id in the configuration file. That value cannot be known before the
/// system runs — it is generated when that person registers — so a first deployment meant
/// starting up with the setting wrong, registering, copying the id out of a bot reply, and
/// restarting. The wrong value in between was not inert: the system came up and refused every
/// trade, twice with a stack trace from deep inside order creation, after the customer's
/// collateral had already been locked.
/// </para>
///
/// <para>
/// The quote already recorded its publisher, so this was never information that needed storing
/// anywhere else. It is also the more accurate answer: a customer trades against the price a
/// particular person posted, so the trade belongs on that person's book. With one administrator
/// the behaviour is identical to before; with several, each keeps their own book and no setting
/// has to be edited.
/// </para>
/// </summary>
public class QuoteFillCounterpartyTests : IDisposable
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal BuyPrice = 17_000_000m;
    private const decimal SellPrice = 17_200_000m;

    private readonly SqliteConnection _connection;
    private readonly Guid _customer = Guid.NewGuid();
    private readonly Guid _firstAdmin = Guid.NewGuid();
    private readonly Guid _secondAdmin = Guid.NewGuid();

    public QuoteFillCounterpartyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    /// <summary>Records every lock so a test can see whose collateral was taken.</summary>
    private sealed class RecordingWallet : StubWalletApiClient
    {
        public List<(Guid UserId, string Asset, decimal Amount)> Locks { get; } = [];

        public override Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
            ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price) =>
            Task.FromResult((true, "ok", true, true));

        public override Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
            Guid userId, string asset, decimal amount)
        {
            Locks.Add((userId, asset, amount));
            return Task.FromResult((true, "locked", (WalletDTO?)new WalletDTO()));
        }

        public override Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount) =>
            Task.FromResult((true, "unlocked"));
    }

    private readonly RecordingWallet _wallet = new();

    /// <summary>
    /// Note what is <b>not</b> here: no market-maker setting of any kind. Dealer mode is the only
    /// thing configured, and the counterparty comes entirely from the data.
    /// </summary>
    private QuoteFillService BuildService()
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

        var orderService = new Orders.Application.OrderService(
            orderRepository, _wallet, new NoOpMatchingEngine(),
            NullLogger<Orders.Application.OrderService>.Instance,
            UsersApiClient: null!,
            new OrderCollateralReconciler(orderRepository, tradeRepository, _wallet,
                NullLogger<OrderCollateralReconciler>.Instance),
            quoteRepository, marketMode);

        return new QuoteFillService(
            quoteRepository, orderService, marketMode,
            new OrderMatchingRepository(context, NullLogger<OrderMatchingRepository>.Instance),
            _wallet, NullLogger<QuoteFillService>.Instance);
    }

    private async Task PublishAsync(Guid publisher)
    {
        await using var db = NewContext();
        db.Quotes.Add(Quote.Publish(Symbol, BuyPrice, SellPrice, publisher));
        await db.SaveChangesAsync();
    }

    private async Task<List<Order>> OrdersAsync()
    {
        await using var db = NewContext();
        return await db.Orders.ToListAsync();
    }

    // ── the counterparty follows the quote ──────────────────────────────────────

    [Fact]
    public async Task TheOrderOnTheOtherSideBelongsToWhoeverPublishedTheQuote()
    {
        await PublishAsync(_firstAdmin);

        await BuildService().AcceptQuoteAsync(_customer, Symbol, OrderSide.Buy, 1m);

        var orders = await OrdersAsync();

        Assert.Contains(orders, o => o.UserId == _customer && o.Side == OrderSide.Buy);
        Assert.Contains(orders, o => o.UserId == _firstAdmin && o.Side == OrderSide.Sell);
    }

    /// <summary>
    /// The point of the change. Two administrators, and the trade lands on the book of the one
    /// whose price the customer actually took — with nothing configured and nothing restarted.
    /// </summary>
    [Fact]
    public async Task WhenAnotherAdminRepublishesTheCounterpartyMovesToThem()
    {
        await PublishAsync(_firstAdmin);
        await PublishAsync(_secondAdmin);   // replaces the first quote

        await BuildService().AcceptQuoteAsync(_customer, Symbol, OrderSide.Sell, 1m);

        var orders = await OrdersAsync();

        Assert.Contains(orders, o => o.UserId == _secondAdmin);
        Assert.DoesNotContain(orders, o => o.UserId == _firstAdmin);
    }

    /// <summary>Collateral is locked against the publisher, not against anybody configured.</summary>
    [Fact]
    public async Task ThePublisherIsTheOneWhoseCollateralIsLocked()
    {
        await PublishAsync(_firstAdmin);

        await BuildService().AcceptQuoteAsync(_customer, Symbol, OrderSide.Buy, 1m);

        Assert.Contains(_wallet.Locks, l => l.UserId == _firstAdmin);
        Assert.Contains(_wallet.Locks, l => l.UserId == _customer);
        Assert.DoesNotContain(_wallet.Locks, l => l.UserId == _secondAdmin);
    }

    /// <summary>
    /// A publisher cannot take their own quote: both sides would be the same account, and
    /// settlement refuses that anyway. Better to say so plainly than to fail later.
    /// </summary>
    [Fact]
    public async Task ThePublisherCannotAcceptTheirOwnQuote()
    {
        await PublishAsync(_firstAdmin);

        var (success, message, _) =
            await BuildService().AcceptQuoteAsync(_firstAdmin, Symbol, OrderSide.Buy, 1m);

        Assert.False(success);
        Assert.Contains("خودش", message);
        Assert.Empty(await OrdersAsync());
    }

    /// <summary>
    /// But a <i>different</i> administrator may — they are a customer of that price like anyone
    /// else, and their own book is separate.
    /// </summary>
    [Fact]
    public async Task AnotherAdminMayTradeAgainstSomebodyElsesQuote()
    {
        await PublishAsync(_firstAdmin);

        var (success, _, _) =
            await BuildService().AcceptQuoteAsync(_secondAdmin, Symbol, OrderSide.Buy, 1m);

        Assert.True(success || (await OrdersAsync()).Count > 0);
    }

    // ── no quote is a hard stop ─────────────────────────────────────────────────

    /// <summary>
    /// With the counterparty coming from the quote, an absent quote means there is nobody to
    /// trade with — so it has to stop before anything is created, rather than being treated as
    /// a detail discovered later.
    /// </summary>
    [Fact]
    public async Task WithNoActiveQuoteTheFillIsRefusedAndNothingIsCreated()
    {
        var (success, message, trade) =
            await BuildService().AcceptQuoteAsync(_customer, Symbol, OrderSide.Buy, 1m);

        Assert.False(success);
        Assert.Null(trade);
        Assert.Contains("مظنه", message);
        Assert.Empty(await OrdersAsync());
        Assert.Empty(_wallet.Locks);
    }

    /// <summary>A symbol that is not in dealer mode is refused before the quote is even read.</summary>
    [Fact]
    public async Task ASymbolThatIsNotInDealerModeIsRefused()
    {
        await PublishAsync(_firstAdmin);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:MarketModes:" + Symbol] = "OrderBook"
            })
            .Build();

        var context = NewContext();
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);
        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);
        var marketMode = new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);

        var service = new QuoteFillService(
            quoteRepository,
            new Orders.Application.OrderService(
                orderRepository, _wallet, new NoOpMatchingEngine(),
                NullLogger<Orders.Application.OrderService>.Instance,
                UsersApiClient: null!,
                new OrderCollateralReconciler(orderRepository, new TradeRepository(context), _wallet,
                    NullLogger<OrderCollateralReconciler>.Instance),
                quoteRepository, marketMode),
            marketMode,
            new OrderMatchingRepository(context, NullLogger<OrderMatchingRepository>.Instance),
            _wallet, NullLogger<QuoteFillService>.Instance);

        var (success, _, _) = await service.AcceptQuoteAsync(_customer, Symbol, OrderSide.Buy, 1m);

        Assert.False(success);
        Assert.Empty(await OrdersAsync());
    }

    private sealed class NoOpMatchingEngine : Orders.Application.IMatchingEngine
    {
        public Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
