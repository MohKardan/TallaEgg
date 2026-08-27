using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// When <c>ExecuteAtomicMatchAsync</c>'s <c>SaveChangesAsync</c> fails, the transaction rolls
/// back on the database side — but step 6 of the method already mutated <c>Status</c> and
/// <c>RemainingAmount</c> on the tracked <c>Order</c> entities in memory, and EF does not
/// revert property values just because the save that would have persisted them failed.
///
/// Hit live: a BTC/IRT match failed on a decimal overflow (Trade.Price didn't fit the old
/// column). <c>QuoteFillService</c>'s cleanup then tried to cancel both orders to release
/// their locked collateral, read them back through the same DbContext, and saw the poisoned
/// in-memory <c>Status = Completed</c> — so the cancel itself threw
/// "سفارشات کامل شده یا رد شده قابل کنسل شدن نیستند", turning a should-have-been-graceful
/// "trade failed" into an unhandled 500 with the customer's funds locked and no cleanup ever
/// run. The overflow itself is fixed separately (Trade.Price/Quantity/QuoteQuantity widened to
/// decimal(28,8)); this covers the underlying pattern, which any other save failure — not just
/// that one — would trigger identically.
/// </summary>
public class MatchFailureRecoveryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal Price = 20_000_000m;
    private const decimal Quantity = 1.5m;

    public MatchFailureRecoveryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var setup = new OrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private Order AddOrder(OrdersDbContext context, Guid userId, OrderSide side, decimal amount)
    {
        var order = Order.CreateMakerOrder(Symbol, amount, Price, userId, side, TradingType.Spot);
        order.Confirm();
        context.Orders.Add(order);
        context.SaveChanges();
        return order;
    }

    /// <summary>A hook immediately before SaveChanges, the same device DoubleMatchTests uses for #42.</summary>
    private sealed class HookedOrdersDbContext : OrdersDbContext
    {
        public Action? BeforeSave;

        public HookedOrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var hook = BeforeSave;
            BeforeSave = null;
            hook?.Invoke();
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task AfterASaveFailure_TheCallersOrderObjectsReflectTheUnchangedPersistedState()
    {
        var hooked = new HookedOrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

        using (hooked)
        {
            var repository = new OrderMatchingRepository(hooked, NullLogger<OrderMatchingRepository>.Instance);

            var buy = AddOrder(hooked, _buyerId, OrderSide.Buy, Quantity);
            var sell = AddOrder(hooked, _sellerId, OrderSide.Sell, Quantity);

            hooked.BeforeSave = () => throw new InvalidOperationException("simulated save failure");

            var (success, trade, error) = await repository.ExecuteAtomicMatchAsync(buy, sell, Quantity);

            Assert.False(success, "a failed save must not be reported as a successful match");
            Assert.Null(trade);

            // The message goes to the customer: QuoteFillService returns this very string from
            // AcceptQuoteAsync, and the bot shows it. So it must be the stable Persian sentence and
            // must not carry the exception's own text — a customer accepting a quote during a
            // database fault used to be shown the .NET message appended to it. The detail is not
            // lost: ExecuteAtomicMatchAsync logs the exception before returning.
            Assert.Equal("خطا در تطبیق سفارشات", error);
            Assert.DoesNotContain("simulated save failure", error);

            // The whole point: these are the exact objects a caller (QuoteFillService) still
            // holds after the failure. If they show the mutation step 6 applied in memory
            // (Completed / RemainingAmount 0) rather than the persisted truth, a cleanup that
            // reads them next — cancelling both orders to release locked collateral — will
            // wrongly refuse, since a Completed order cannot be cancelled.
            Assert.Equal(OrderStatus.Confirmed, buy.Status);
            Assert.Equal(OrderStatus.Confirmed, sell.Status);
            Assert.Equal(Quantity, buy.RemainingAmount);
            Assert.Equal(Quantity, sell.RemainingAmount);
        }

        // And nothing was actually persisted either.
        using var verify = new OrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);
        Assert.Equal(0, await verify.Trades.CountAsync());
        Assert.All(await verify.Orders.ToListAsync(), o => Assert.Equal(OrderStatus.Confirmed, o.Status));
    }
}
