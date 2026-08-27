using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// An unconfirmed order must not enter matching.
///
/// This is the root of audit finding C-5. An order is <c>Pending</c> from the moment it is saved,
/// which is before its collateral is locked. The order-book queries used to return <c>Pending</c>
/// orders too, so the background loop — running every second — could pick up an order with nothing
/// backing it and record a trade that could never settle.
///
/// The order is now lock, then confirm, then match, and only <c>Confirmed</c> is visible — so "no
/// trade exists before its collateral is locked" is a structural guarantee rather than a
/// behavioural contract the code has to remember to honour.
/// </summary>
public class PendingOrderNotMatchableTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly OrdersDbContext _context;
    private readonly OrderMatchingRepository _repository;

    private const string Symbol = "MAUA/IRT";

    public PendingOrderNotMatchableTests()
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

    /// <summary>Creates an order in the desired state. Without a confirm it stays Pending.</summary>
    private Order AddOrder(OrderSide side, bool confirm)
    {
        var order = Order.CreateMakerOrder(Symbol, 10m, 20_000_000m, Guid.NewGuid(), side, TradingType.Spot);
        if (confirm) order.Confirm();
        _context.Orders.Add(order);
        _context.SaveChanges();
        return order;
    }

    // A note on coverage: GetBuyOrdersWithLockAsync and GetSellOrdersWithLockAsync are not tested
    // directly here, because they sort with OrderBy(o => o.Price) and SQLite does not support
    // ordering on decimal, which is fine on SQL Server. Both use the same shared Matchable
    // expression that the GetActiveAssetsAsync test below covers. This is a limitation of the test
    // environment rather than the code, and is recorded in #46.

    /// <summary>
    /// The background loop decides which assets to scan from this list. If a Pending order appears
    /// here, the engine wakes up to work on an order that has no collateral yet.
    /// </summary>
    [Fact]
    public async Task PendingOrders_DoNotMakeAnAssetLookActive()
    {
        AddOrder(OrderSide.Buy, confirm: false);

        Assert.Empty(await _repository.GetActiveAssetsAsync());
    }

    /// <summary>A confirmed order must still mark the asset active — otherwise the test above would
    /// stay green under "nothing is ever active".</summary>
    [Fact]
    public async Task ConfirmedOrders_DoMakeAnAssetActive()
    {
        AddOrder(OrderSide.Buy, confirm: true);

        Assert.Single(await _repository.GetActiveAssetsAsync());
    }

    /// <summary>
    /// Defence in depth: even if an order reaches matching by another route, the in-transaction
    /// re-check must refuse it. This keeps the query filter and the atomic re-check from drifting
    /// apart.
    ///
    /// The match quantity is deliberately <b>less</b> than the order quantity, producing a partial
    /// fill. On a complete fill, <c>Order.Complete()</c> throws against a Pending order and the trade
    /// is rolled back incidentally — so the test would stay green even without the fix and prove
    /// nothing. The partial-fill path has no such accidental guard, so only this case actually
    /// exercises the filter.
    /// </summary>
    [Fact]
    public async Task APartialMatchAgainstAPendingOrder_IsRejectedInsideTheTransaction()
    {
        var buy = AddOrder(OrderSide.Buy, confirm: true);
        var pendingSell = AddOrder(OrderSide.Sell, confirm: false);

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, pendingSell, 4m);

        Assert.False(success, "an unconfirmed order must not be settled against");
        Assert.Null(trade);
        Assert.Empty(_context.Trades);

        // A specific message, so a refusal for some other reason cannot be mistaken for this one.
        Assert.Contains("وضعیت سفارشات", error);
    }

    /// <summary>
    /// And the normal path still works — otherwise the tests above would stay green under "nothing
    /// ever matches".
    /// </summary>
    [Fact]
    public async Task TwoConfirmedOrders_StillMatch()
    {
        var buy = AddOrder(OrderSide.Buy, confirm: true);
        var sell = AddOrder(OrderSide.Sell, confirm: true);

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, sell, 10m);

        Assert.True(success, error);
        Assert.NotNull(trade);
    }
}
