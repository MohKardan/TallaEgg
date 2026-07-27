using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace Wallet.Tests;

/// <summary>
/// Tests that a matched trade records the buyer and the seller the right way round.
///
/// This covers a bug that reached production data: ExecuteAtomicMatchAsync takes
/// (buyOrder, sellOrder), but callers passed (makerOrder, takerOrder). Both parameters
/// are <see cref="Order"/>, so the mistake compiled silently and only inverted the roles
/// when the resting order happened to be a sell. The trade was then settled in the wrong
/// direction — the seller received the gold and the buyer received the money.
///
/// The existing settlement tests could not catch this because they pass buyerUserId and
/// sellerUserId directly, i.e. below the layer where the inversion happened.
/// </summary>
public class AtomicMatchRoleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly OrdersDbContext _context;
    private readonly OrderMatchingRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Symbol = "MAUA/IRT";
    private const decimal Price = 20_000_000m;
    private const decimal Quantity = 10m;

    public AtomicMatchRoleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new OrdersDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new OrderMatchingRepository(_context, NullLogger<OrderMatchingRepository>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Order AddOrder(Guid userId, OrderSide side, DateTime createdAt)
    {
        var order = Order.CreateMakerOrder(Symbol, Quantity, Price, userId, side, TradingType.Spot);
        // Force the timestamp so which side is the resting (maker) order is deterministic.
        typeof(Order).GetProperty(nameof(Order.CreatedAt))!
            .SetValue(order, createdAt);
        order.Confirm();
        _context.Orders.Add(order);
        _context.SaveChanges();
        return order;
    }

    /// <summary>
    /// The resting order is the SELL — the exact case that used to invert the roles.
    /// </summary>
    [Fact]
    public async Task ExecuteAtomicMatch_WhenSellOrderIsResting_RecordsRolesCorrectly()
    {
        var sell = AddOrder(_sellerId, OrderSide.Sell, DateTime.UtcNow.AddMinutes(-5)); // resting
        var buy = AddOrder(_buyerId, OrderSide.Buy, DateTime.UtcNow);                   // incoming

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, sell, Quantity);

        Assert.True(success, error);
        Assert.NotNull(trade);
        Assert.Equal(_buyerId, trade!.BuyerUserId);
        Assert.Equal(_sellerId, trade.SellerUserId);
        Assert.Equal(buy.Id, trade.BuyOrderId);
        Assert.Equal(sell.Id, trade.SellOrderId);
    }

    /// <summary>The mirror case: the resting order is the BUY.</summary>
    [Fact]
    public async Task ExecuteAtomicMatch_WhenBuyOrderIsResting_RecordsRolesCorrectly()
    {
        var buy = AddOrder(_buyerId, OrderSide.Buy, DateTime.UtcNow.AddMinutes(-5));  // resting
        var sell = AddOrder(_sellerId, OrderSide.Sell, DateTime.UtcNow);              // incoming

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, sell, Quantity);

        Assert.True(success, error);
        Assert.Equal(_buyerId, trade!.BuyerUserId);
        Assert.Equal(_sellerId, trade.SellerUserId);
    }

    /// <summary>
    /// The structural guard: passing the orders the wrong way round must fail loudly
    /// instead of recording an inverted trade.
    /// </summary>
    [Fact]
    public async Task ExecuteAtomicMatch_WhenOrdersArePassedSwapped_IsRejected()
    {
        var buy = AddOrder(_buyerId, OrderSide.Buy, DateTime.UtcNow.AddMinutes(-5));
        var sell = AddOrder(_sellerId, OrderSide.Sell, DateTime.UtcNow);

        // Deliberately swapped — this is what the buggy callers were doing.
        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(sell, buy, Quantity);

        Assert.False(success, "a swapped call must be rejected, not recorded");
        Assert.Null(trade);
        Assert.Contains("سفارش خرید نامعتبر است", error);
        Assert.Empty(_context.Trades);
    }
}
