using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;

namespace Wallet.Tests;

/// <summary>
/// One pair of orders must produce one trade, for no more than the order's own amount
/// (issue #74).
///
/// A single quote acceptance produced two trades from one order pair and the customer paid
/// for both. Orders for 1.23 g yielded trades of 1.2345 g and 1.23 g — 2.4645 g in total.
/// Three defects combined:
///
/// <list type="number">
/// <item>the quantity columns hold two decimal places, so a customer's 1.2345 was truncated
/// to 1.23 on write while the in-memory entity kept 1.2345;</item>
/// <item><c>ExecuteAtomicMatchAsync</c>'s "re-fetch with lock" did neither — EF identity
/// resolution returned the stale tracked entity, and there was no lock or concurrency
/// token;</item>
/// <item>the dealer fill path matched outside the semaphore that serialises the background
/// engine, so both could match the same pair at once.</item>
/// </list>
///
/// Conservation checks could not catch any of it: both trades were internally balanced, so
/// no money was created. The cost fell on the customer, which nothing was measuring.
/// </summary>
public class DoubleMatchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly OrdersDbContext _context;
    private readonly OrderMatchingRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal Price = 16_967_542.36m;

    public DoubleMatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;
        _context = new OrdersDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new OrderMatchingRepository(_context, NullLogger<OrderMatchingRepository>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Order AddOrder(Guid userId, OrderSide side, decimal amount)
    {
        var order = Order.CreateMakerOrder(Symbol, amount, Price, userId, side, TradingType.Spot);
        order.Confirm();
        _context.Orders.Add(order);
        _context.SaveChanges();
        return order;
    }

    /// <summary>
    /// A second context over the same database, which is what a concurrent matcher has: its
    /// own change tracker, so it reads the row rather than a tracked copy.
    /// </summary>
    private OrderMatchingRepository SeparateSession(out OrdersDbContext context)
    {
        context = new OrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

        return new OrderMatchingRepository(context, NullLogger<OrderMatchingRepository>.Instance);
    }

    // ── one pair, one trade ─────────────────────────────────────────────────────

    /// <summary>
    /// The defect exactly as it happened: two matchers reach the same pair, each having read
    /// the remaining amount before either wrote. Only one may produce a trade.
    ///
    /// Driven through two DbContexts rather than two threads on purpose. What has to be true
    /// is not "a race can occur" — it demonstrably can — but "when it does, at most one trade
    /// exists afterwards". Two sessions make that deterministic; two threads on one SQLite
    /// connection would be flaky or deadlock and would prove less.
    /// </summary>
    [Fact]
    public async Task TwoMatchersOnTheSamePair_ProduceExactlyOneTrade()
    {
        var buy = AddOrder(_buyerId, OrderSide.Buy, 1.23m);
        var sell = AddOrder(_sellerId, OrderSide.Sell, 1.23m);

        var other = SeparateSession(out var otherContext);
        using (otherContext)
        {
            var otherBuy = await otherContext.Orders.SingleAsync(o => o.Id == buy.Id);
            var otherSell = await otherContext.Orders.SingleAsync(o => o.Id == sell.Id);

            var first = await _repository.ExecuteAtomicMatchAsync(buy, sell, 1.23m);
            var second = await other.ExecuteAtomicMatchAsync(otherBuy, otherSell, 1.23m);

            Assert.True(first.Success, first.ErrorMessage);
            Assert.False(second.Success,
                "the second matcher must be refused; the pair was already fully matched");
        }

        _context.ChangeTracker.Clear();
        Assert.Equal(1, await _context.Trades.CountAsync());
    }

    /// <summary>
    /// The consequence that actually cost money: the total quantity traded across a pair can
    /// never exceed what was ordered. This is the assertion the conservation checks were
    /// missing — money stayed balanced while the customer bought twice.
    /// </summary>
    [Fact]
    public async Task TheTotalTradedNeverExceedsTheOrderAmount()
    {
        var buy = AddOrder(_buyerId, OrderSide.Buy, 1.23m);
        var sell = AddOrder(_sellerId, OrderSide.Sell, 1.23m);

        var other = SeparateSession(out var otherContext);
        using (otherContext)
        {
            var otherBuy = await otherContext.Orders.SingleAsync(o => o.Id == buy.Id);
            var otherSell = await otherContext.Orders.SingleAsync(o => o.Id == sell.Id);

            await _repository.ExecuteAtomicMatchAsync(buy, sell, 1.23m);
            await other.ExecuteAtomicMatchAsync(otherBuy, otherSell, 1.23m);
        }

        _context.ChangeTracker.Clear();
        var traded = await _context.Trades.SumAsync(t => t.Quantity);

        Assert.Equal(1.23m, traded);
    }

    // ── a match may not exceed what the database holds ───────────────────────────

    /// <summary>
    /// A match may not be finer than the asset's own precision.
    ///
    /// This is the half of the defect that came from column precision. `Orders.Amount` and
    /// `Orders.RemainingAmount` hold two decimal places while `Trades.Quantity` holds eight,
    /// so a customer's 1.2345 was truncated to 1.23 in the order row and stored in full in
    /// the trade row — a trade larger than the order that funded it.
    ///
    /// <para>
    /// <b>Why the assertion is about rounding rather than truncation:</b> SQLite does not
    /// enforce decimal precision, so the truncation itself cannot be reproduced here — the
    /// first version of this test asserted the row held 1.23 and failed, because under SQLite
    /// it holds 1.2345. The truncation is evidenced by the production rows in #74. What is
    /// provider-independent, and what actually prevents the defect, is that the match rounds
    /// to the asset's precision so the order, the trade and the settlement carry one number.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AMatchIsRoundedToTheAssetsPrecision()
    {
        var buy = AddOrder(_buyerId, OrderSide.Buy, 1.2345m);
        var sell = AddOrder(_sellerId, OrderSide.Sell, 1.2345m);

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, sell, 1.2345m);

        Assert.True(success, error);

        // Gold is declared at two decimal places, the same as the order columns.
        var expected = CurrenciesConstant.RoundToCurrencyPrecision(1.2345m, CurrenciesConstant.Maua);
        Assert.Equal(1.23m, expected);
        Assert.Equal(expected, trade!.Quantity);
    }

    /// <summary>
    /// The ordinary case must keep working. A guard that also blocks legitimate fills would
    /// be a worse defect than the one being fixed.
    /// </summary>
    [Fact]
    public async Task AnOrdinarySingleMatch_StillSucceeds()
    {
        var buy = AddOrder(_buyerId, OrderSide.Buy, 2.5m);
        var sell = AddOrder(_sellerId, OrderSide.Sell, 2.5m);

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, sell, 2.5m);

        Assert.True(success, error);
        Assert.Equal(2.5m, trade!.Quantity);
    }

    /// <summary>
    /// The concurrency token specifically.
    ///
    /// <para>
    /// The other tests here pass on the reload alone — verified by removing the token and
    /// watching them stay green. They run read→write, read→write, so the second matcher's
    /// reload already sees a spent order. The token covers the window the reload cannot: the
    /// row changing between our read and our write.
    /// </para>
    ///
    /// <para>
    /// That divergence is staged by setting the tracked entity's <i>original</i> value, rather
    /// than by running a second transaction. In-memory SQLite on one connection rejects nested
    /// transactions outright ("SqliteConnection does not support nested transactions"), and a
    /// second connection would block on the write lock this transaction already holds. Setting
    /// the original value produces the identical condition — EF emits
    /// <c>WHERE RemainingAmount = &lt;what we think we read&gt;</c>, no row matches, and the
    /// update is refused — which is the token's entire contract.
    /// </para>
    /// </summary>
    [Fact]
    public async Task WhenTheRowNoLongerMatchesWhatWeRead_TheMatchIsRefused()
    {
        var hooked = new HookedOrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

        using (hooked)
        {
            var repository = new OrderMatchingRepository(hooked, NullLogger<OrderMatchingRepository>.Instance);

            var buy = AddOrder(_buyerId, OrderSide.Buy, 1.23m);
            var sell = AddOrder(_sellerId, OrderSide.Sell, 1.23m);

            var hookedBuy = await hooked.Orders.SingleAsync(o => o.Id == buy.Id);
            var hookedSell = await hooked.Orders.SingleAsync(o => o.Id == sell.Id);

            // Just before the write, pretend we had read a value the row does not hold — the
            // state a rival's commit would have left us in.
            hooked.BeforeSave = () =>
                hooked.Entry(hookedBuy).Property(o => o.RemainingAmount).OriginalValue = 0.5m;

            var (success, _, error) = await repository.ExecuteAtomicMatchAsync(hookedBuy, hookedSell, 1.23m);

            Assert.False(success, "a write whose read is out of date must be refused");
            Assert.Contains("هم‌زمان", error!);
        }

        // Nothing was written: no trade, and the orders are untouched.
        _context.ChangeTracker.Clear();
        Assert.Equal(0, await _context.Trades.CountAsync());
        Assert.Equal(1.23m, (await _context.Orders.SingleAsync(o => o.Side == OrderSide.Buy)).RemainingAmount);
    }

    /// <summary>
    /// A hook immediately before <c>SaveChanges</c>, the same device used for #42.
    /// </summary>
    private sealed class HookedOrdersDbContext : OrdersDbContext
    {
        public Action? BeforeSave;

        public HookedOrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var hook = BeforeSave;
            BeforeSave = null; // once only — a rollback would otherwise run it again
            hook?.Invoke();
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Partial fills must still work: two sequential matches on one large order are correct
    /// behaviour, unlike two matches for the same fill.
    /// </summary>
    [Fact]
    public async Task SequentialPartialFills_AreStillAllowed()
    {
        var buy = AddOrder(_buyerId, OrderSide.Buy, 10m);
        var sellA = AddOrder(_sellerId, OrderSide.Sell, 4m);
        var sellB = AddOrder(_sellerId, OrderSide.Sell, 6m);

        var first = await _repository.ExecuteAtomicMatchAsync(buy, sellA, 4m);
        var second = await _repository.ExecuteAtomicMatchAsync(buy, sellB, 6m);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage);

        _context.ChangeTracker.Clear();
        Assert.Equal(10m, await _context.Trades.SumAsync(t => t.Quantity));
        Assert.Equal(0m, (await _context.Orders.SingleAsync(o => o.Id == buy.Id)).RemainingAmount);
    }
}
