using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
/// An order whose confirmation fails after its collateral was locked gives that collateral back
/// (issue #331).
///
/// <para>
/// <b>What was wrong:</b> <c>OrderService.CreateLockedAndConfirmedOrderAsync</c> saves the order as
/// Pending, locks the collateral, then confirms. When the confirmation failed it logged a warning
/// and returned, so the order stayed Pending for good with its collateral locked, and nothing in
/// the Orders service ever cancels, expires or unlocks a Pending order. On the quote path that left
/// the customer told the fill had failed while their money stayed locked.
/// </para>
///
/// <para>
/// The fault is injected where a real one happens: the database refuses the confirmation's save,
/// once, after the wallet has already taken the lock. That is the <c>catch (Exception)</c> in
/// <c>ConfirmOrderIfPendingAsync</c> turning a transient fault into a plain <c>false</c>.
/// </para>
/// </summary>
public class UnconfirmedOrderReleasesCollateralTests : IDisposable
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal BuyPrice = 17_000_000m;
    private const decimal SellPrice = 17_200_000m;
    private const decimal Quantity = 1m;

    private readonly SqliteConnection _connection;
    private readonly FailNextSave _faults = new();
    private readonly LedgerWallet _wallet;
    private readonly Guid _customer = Guid.NewGuid();
    private readonly Guid _dealer = Guid.NewGuid();

    public UnconfirmedOrderReleasesCollateralTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();

        _wallet = new LedgerWallet(_faults);
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_faults)
            .Options);

    // ── POST /api/orders ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrderAsync_ConfirmationFailsAfterTheLock_ReleasesTheLock()
    {
        _wallet.FailConfirmationAfterLockNumber = 1;

        await BuildOrderService(NewContext()).CreateOrderAsync(new OrderDto
        {
            Asset = Symbol,
            Amount = Quantity,
            Price = BuyPrice,
            UserId = _customer,
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            TradingType = TradingType.Spot
        });

        Assert.True(_faults.Fired, "the injected fault never fired, so nothing was tested");
        Assert.Equal(0m, _wallet.LockedOf(_customer, CurrenciesConstant.Toman));
    }

    [Fact]
    public async Task CreateOrderAsync_ConfirmationFailsAfterTheLock_LeavesNoPendingOrder()
    {
        _wallet.FailConfirmationAfterLockNumber = 1;

        await BuildOrderService(NewContext()).CreateOrderAsync(new OrderDto
        {
            Asset = Symbol,
            Amount = Quantity,
            Price = BuyPrice,
            UserId = _customer,
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            TradingType = TradingType.Spot
        });

        var order = Assert.Single(await OrdersOfAsync(_customer));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    // ── the quote fill ──────────────────────────────────────────────────────────

    /// <summary>
    /// The case that harms a real customer: their side is created first, so when it fails to
    /// confirm the method returns before the dealer's side exists, and nothing else would have
    /// cancelled it.
    /// </summary>
    [Theory]
    [InlineData(OrderSide.Buy, CurrenciesConstant.Toman)]
    [InlineData(OrderSide.Sell, CurrenciesConstant.Maua)]
    public async Task AcceptQuoteAsync_CustomersSideFailsToConfirm_ReleasesTheCustomersLock(
        OrderSide customerSide, string collateralAsset)
    {
        await PublishQuoteAsync();
        _wallet.FailConfirmationAfterLockNumber = 1;

        var (success, _, _) = await BuildQuoteFillService().AcceptQuoteAsync(_customer, Symbol, customerSide, Quantity);

        Assert.False(success);
        Assert.True(_faults.Fired, "the injected fault never fired, so nothing was tested");
        Assert.Equal(0m, _wallet.LockedOf(_customer, collateralAsset));
        Assert.Equal(OrderStatus.Cancelled, Assert.Single(await OrdersOfAsync(_customer)).Status);
    }

    /// <summary>
    /// The dealer's side fails to confirm. The quote path already cancelled the customer's order in
    /// this case; the dealer's own order was left Pending with the shop's collateral locked behind
    /// it, which cannot stop the shop trading (decision 003) but leaves a lock with no live order.
    /// </summary>
    [Theory]
    [InlineData(OrderSide.Buy, CurrenciesConstant.Toman, CurrenciesConstant.Maua)]
    [InlineData(OrderSide.Sell, CurrenciesConstant.Maua, CurrenciesConstant.Toman)]
    public async Task AcceptQuoteAsync_DealersSideFailsToConfirm_ReleasesBothLocks(
        OrderSide customerSide, string customerCollateral, string dealerCollateral)
    {
        await PublishQuoteAsync();
        _wallet.FailConfirmationAfterLockNumber = 2;

        var (success, _, _) = await BuildQuoteFillService().AcceptQuoteAsync(_customer, Symbol, customerSide, Quantity);

        Assert.False(success);
        Assert.True(_faults.Fired, "the injected fault never fired, so nothing was tested");
        Assert.Equal(0m, _wallet.LockedOf(_customer, customerCollateral));
        Assert.Equal(0m, _wallet.LockedOf(_dealer, dealerCollateral));
        Assert.Equal(OrderStatus.Cancelled, Assert.Single(await OrdersOfAsync(_dealer)).Status);
    }

    // ── what the fix must not do ────────────────────────────────────────────────

    /// <summary>
    /// A failed confirmation does not always mean the lock is unowned: it also means the order has
    /// already left Pending. Here something else marked it Failed between the lock and the
    /// confirmation, and a Failed order is not ours to cancel or unlock.
    /// </summary>
    /// <remarks>
    /// This cannot go red on the unfixed code, which released nothing in any case. It is the guard
    /// against the fix the audit suggested first — unlock whenever confirmation returns false —
    /// which would release collateral blind.
    /// </remarks>
    [Fact]
    public async Task CreateOrderAsync_OrderLeftPendingBeforeConfirmation_UnlocksNothingBlind()
    {
        // Written through the service's own context: it already tracks the order, so a write from
        // another context would never be seen by the confirmation's read.
        var context = NewContext();
        _wallet.OnLock = orderOwner => MarkOnlyOrderFailedAsync(context, orderOwner);

        await BuildOrderService(context).CreateOrderAsync(new OrderDto
        {
            Asset = Symbol,
            Amount = Quantity,
            Price = BuyPrice,
            UserId = _customer,
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            TradingType = TradingType.Spot
        });

        Assert.Equal(OrderStatus.Failed, Assert.Single(await OrdersOfAsync(_customer)).Status);
        Assert.Empty(_wallet.Unlocks);
    }

    /// <summary>
    /// The database is still refusing writes when the cancellation runs. The lock cannot be given
    /// back then, and nothing in this method can change that; what it must do is not turn the
    /// failure into an exception for the caller, and leave a CRITICAL line that names the amount.
    /// </summary>
    [Fact]
    public async Task CreateOrderAsync_CancellationAlsoFails_DoesNotThrowAndLogsTheStrandedLock()
    {
        _wallet.FailConfirmationAfterLockNumber = 1;
        _wallet.FailingSaves = 2;
        var logger = new CapturingLogger<OrderService>();

        await BuildOrderService(NewContext(), logger).CreateOrderAsync(new OrderDto
        {
            Asset = Symbol,
            Amount = Quantity,
            Price = BuyPrice,
            UserId = _customer,
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            TradingType = TradingType.Spot
        });

        Assert.Equal(0, _faults.Remaining);
        Assert.Equal(BuyPrice, _wallet.LockedOf(_customer, CurrenciesConstant.Toman));
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Error && e.Message.StartsWith("CRITICAL") && e.Message.Contains("17000000"));
    }

    // ── harness ─────────────────────────────────────────────────────────────────

    private static async Task MarkOnlyOrderFailedAsync(OrdersDbContext db, Guid userId)
    {
        var order = await db.Orders.SingleAsync(o => o.UserId == userId);
        order.Fail("marked failed by another writer");
        await db.SaveChangesAsync();
    }

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

    /// <summary>
    /// Once armed, throws from the next <paramref name="saves"/> <c>SaveChanges</c> calls, then lets
    /// them through: a transient fault, so by default the cancellation that follows succeeds the way
    /// it would once the database answers again.
    /// </summary>
    private sealed class FailNextSave : SaveChangesInterceptor
    {
        public int Remaining { get; private set; }

        public bool Fired { get; private set; }

        public void Arm(int saves) => Remaining = saves;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Remaining > 0)
            {
                Remaining--;
                Fired = true;
                throw new FakeDbException("injected: the database refused the save");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FakeDbException(string message) : DbException(message);

    /// <summary>
    /// Approves every check, and keeps the running locked total per user and asset so a test can
    /// assert what is still held rather than which calls were made.
    /// </summary>
    private sealed class LedgerWallet(FailNextSave faults) : StubWalletApiClient
    {
        private readonly Dictionary<(Guid, string), decimal> _locked = new();
        private int _locks;

        /// <summary>Arm the database fault right after this lock (1-based) succeeds.</summary>
        public int? FailConfirmationAfterLockNumber { get; set; }

        /// <summary>How many saves in a row the database refuses once the fault is armed.</summary>
        public int FailingSaves { get; set; } = 1;

        public Func<Guid, Task>? OnLock { get; set; }

        public List<(Guid UserId, string Asset, decimal Amount)> Unlocks { get; } = new();

        public decimal LockedOf(Guid userId, string asset) => _locked.GetValueOrDefault((userId, asset));

        public override Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
            ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price) =>
            Task.FromResult((true, "ok", true, true));

        public override async Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
            Guid userId, string asset, decimal amount)
        {
            _locked[(userId, asset)] = LockedOf(userId, asset) + amount;

            if (++_locks == FailConfirmationAfterLockNumber)
                faults.Arm(FailingSaves);

            if (OnLock is not null)
                await OnLock(userId);

            return (true, "locked", new WalletDTO());
        }

        public override Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
        {
            _locked[(userId, asset)] = LockedOf(userId, asset) - amount;
            Unlocks.Add((userId, asset, amount));
            return Task.FromResult((true, "unlocked"));
        }
    }

    private OrderService BuildOrderService(OrdersDbContext context, ILogger<OrderService>? logger = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:MarketModes:" + Symbol] = "Dealer",
                ["UsersApiUrl"] = "http://users.test/api"
            })
            .Build();

        var marketMode = new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);
        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);
        var tradeRepository = new TradeRepository(context);

        // 404 for every user: not an administrator, so the ordinary customer path runs.
        var usersApiClient = new UsersApiClient(
            new HttpClient(new NotFoundHandler()), configuration, NullLogger<UsersApiClient>.Instance);

        return new OrderService(
            orderRepository, _wallet, new NoOpMatchingEngine(),
            logger ?? NullLogger<OrderService>.Instance,
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

        return new QuoteFillService(
            quoteRepository, BuildOrderService(context), marketMode,
            new OrderMatchingRepository(context, NullLogger<OrderMatchingRepository>.Instance),
            _wallet, NullLogger<QuoteFillService>.Instance);
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"success\":false}", System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class NoOpMatchingEngine : IMatchingEngine
    {
        public Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
