using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Integration tests for <see cref="PositionService"/> against real (SQLite-backed)
/// <see cref="TradeRepository"/> and <see cref="QuoteRepository"/> — the FIFO arithmetic
/// itself is pinned down in <c>PositionCalculatorTests</c>; this covers wiring trades and
/// the active quote into that engine correctly (issue #93).
///
/// Every trade here is between "the customer" and "the house" — the dealer/admin side of
/// every quote-fill trade. Several tests deliberately query positions for the house's own
/// user id instead of the customer's, to prove the same service needs no special-casing to
/// produce the shop's own P&amp;L: it is just another participant's trades.
/// </summary>
public class PositionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly OrdersDbContext _context;
    private readonly PositionService _service;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _houseId = Guid.NewGuid();

    private const string Symbol = "MAUA/IRT";

    public PositionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;
        _context = new OrdersDbContext(options);
        _context.Database.EnsureCreated();

        _service = new PositionService(
            new TradeRepository(_context),
            new QuoteRepository(_context, NullLogger<QuoteRepository>.Instance));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Order SeedOrder(Guid userId, OrderSide side, decimal qty, decimal price, string symbol)
    {
        var order = Order.CreateMakerOrder(symbol, qty, price, userId, side, TradingType.Spot);
        order.Confirm();
        _context.Orders.Add(order);
        _context.SaveChanges();
        return order;
    }

    /// <summary>Seeds one trade between the customer and the house, on whichever side is asked for.</summary>
    private void SeedTrade(bool customerIsBuyer, decimal qty, decimal price, DateTime createdAt,
        decimal feeBuyer = 0m, decimal feeSeller = 0m, string symbol = Symbol)
    {
        var buyerId = customerIsBuyer ? _customerId : _houseId;
        var sellerId = customerIsBuyer ? _houseId : _customerId;

        var buyOrder = SeedOrder(buyerId, OrderSide.Buy, qty, price, symbol);
        var sellOrder = SeedOrder(sellerId, OrderSide.Sell, qty, price, symbol);

        var trade = Trade.Create(
            buyOrder.Id, sellOrder.Id, buyOrder.Id, sellOrder.Id,
            symbol, price, qty, qty * price,
            buyerId, sellerId, buyerId, sellerId,
            feeBuyer: feeBuyer, feeSeller: feeSeller);

        // Trade.Create stamps CreatedAt = UtcNow; FIFO order depends on it, so tests need
        // control over it the same way AtomicMatchRoleTests controls Order.CreatedAt.
        typeof(Trade).GetProperty(nameof(Trade.CreatedAt))!.SetValue(trade, createdAt);

        _context.Trades.Add(trade);
        _context.SaveChanges();
    }

    private void SeedQuote(decimal buyPrice, decimal sellPrice, string symbol = Symbol)
    {
        var quote = Quote.Publish(symbol, buyPrice, sellPrice, _houseId);
        _context.Quotes.Add(quote);
        _context.SaveChanges();
    }

    [Fact]
    public async Task ACustomerWithNoTrades_HasNoPositions()
    {
        var result = await _service.GetPositionsAsync(_customerId);

        Assert.Empty(result.Positions);
        Assert.Equal(0m, result.TotalRealizedPnl);
        Assert.Equal(0m, result.TotalUnrealizedPnl);
    }

    [Fact]
    public async Task ABuyThenAMatchingSell_RealizesTheGainAndLeavesNoOpenPosition()
    {
        SeedTrade(customerIsBuyer: true, qty: 5m, price: 100m, createdAt: DateTime.UtcNow.AddDays(-2));
        SeedTrade(customerIsBuyer: false, qty: 5m, price: 150m, createdAt: DateTime.UtcNow.AddDays(-1));

        var result = await _service.GetPositionsAsync(_customerId);

        var position = Assert.Single(result.Positions);
        Assert.Equal(Symbol, position.Symbol);
        Assert.Equal(0m, position.Quantity);
        Assert.Null(position.AverageCost);
        Assert.Equal(250m, position.RealizedPnl); // 5 * (150 - 100)
        Assert.Equal(0m, position.UnrealizedPnl);
        Assert.Equal(250m, result.TotalRealizedPnl);
    }

    [Fact]
    public async Task AnOpenPosition_IsMarkedAgainstTheActiveQuotesBuyPrice()
    {
        SeedTrade(customerIsBuyer: true, qty: 10m, price: 100m, createdAt: DateTime.UtcNow.AddDays(-1));
        SeedQuote(buyPrice: 120m, sellPrice: 125m);

        var result = await _service.GetPositionsAsync(_customerId);

        var position = Assert.Single(result.Positions);
        Assert.Equal(10m, position.Quantity);
        Assert.Equal(100m, position.AverageCost);
        Assert.Equal(120m, position.MarkPrice); // the buy price -- what selling now would pay, not the sell price
        Assert.Equal(200m, position.UnrealizedPnl); // 10 * (120 - 100)
    }

    [Fact]
    public async Task AnOpenPositionWithNoPublishedQuote_HasNoComputableUnrealizedPnl()
    {
        SeedTrade(customerIsBuyer: true, qty: 10m, price: 100m, createdAt: DateTime.UtcNow.AddDays(-1));

        var result = await _service.GetPositionsAsync(_customerId);

        var position = Assert.Single(result.Positions);
        Assert.Null(position.MarkPrice);
        Assert.Null(position.UnrealizedPnl);
        Assert.Equal(0m, result.TotalUnrealizedPnl); // a missing quote must not corrupt the total
    }

    [Fact]
    public async Task ACreditBackedShortPosition_ProducesTheCorrectSign()
    {
        SeedTrade(customerIsBuyer: false, qty: 4m, price: 100m, createdAt: DateTime.UtcNow.AddDays(-1)); // customer sells short
        SeedQuote(buyPrice: 70m, sellPrice: 75m); // price fell after shorting -- a gain

        var result = await _service.GetPositionsAsync(_customerId);

        var position = Assert.Single(result.Positions);
        Assert.Equal(-4m, position.Quantity);
        Assert.Equal(100m, position.AverageCost);
        Assert.Equal(70m, position.MarkPrice);
        Assert.Equal(120m, position.UnrealizedPnl); // -4 * (70 - 100)
    }

    [Fact]
    public async Task FeesAreReadFromTheTradeRecord_NotAssumedZero()
    {
        SeedTrade(customerIsBuyer: true, qty: 5m, price: 100m, createdAt: DateTime.UtcNow.AddDays(-2), feeBuyer: 5m);
        SeedTrade(customerIsBuyer: false, qty: 5m, price: 150m, createdAt: DateTime.UtcNow.AddDays(-1), feeSeller: 7m);

        var result = await _service.GetPositionsAsync(_customerId);

        var position = Assert.Single(result.Positions);
        Assert.Equal(250m - 12m, position.RealizedPnl);
    }

    [Fact]
    public async Task TotalsSumAcrossEveryTradedSymbol()
    {
        SeedTrade(customerIsBuyer: true, qty: 5m, price: 100m, createdAt: DateTime.UtcNow.AddDays(-4));
        SeedTrade(customerIsBuyer: false, qty: 5m, price: 150m, createdAt: DateTime.UtcNow.AddDays(-3)); // +250 realized, MAUA/IRT

        SeedTrade(customerIsBuyer: true, qty: 1m, price: 900_000_000m, createdAt: DateTime.UtcNow.AddDays(-2), symbol: "BTC/IRT");
        SeedTrade(customerIsBuyer: false, qty: 1m, price: 800_000_000m, createdAt: DateTime.UtcNow.AddDays(-1), symbol: "BTC/IRT"); // -100,000,000 realized, BTC/IRT

        var result = await _service.GetPositionsAsync(_customerId);

        Assert.Equal(2, result.Positions.Count);
        Assert.Equal(250m - 100_000_000m, result.TotalRealizedPnl);
    }

    /// <summary>
    /// The house is just the other side of every one of the customer's trades above. Its
    /// realized P&amp;L must be the exact mirror image -- proving the service needs no
    /// separate "admin" code path, only the admin's own user id.
    /// </summary>
    [Fact]
    public async Task TheHousesOwnPositions_AreTheExactMirrorOfTheCustomers()
    {
        SeedTrade(customerIsBuyer: true, qty: 5m, price: 100m, createdAt: DateTime.UtcNow.AddDays(-2));
        SeedTrade(customerIsBuyer: false, qty: 5m, price: 150m, createdAt: DateTime.UtcNow.AddDays(-1));

        var customerResult = await _service.GetPositionsAsync(_customerId);
        var houseResult = await _service.GetPositionsAsync(_houseId);

        Assert.Equal(customerResult.TotalRealizedPnl, -houseResult.TotalRealizedPnl);
    }
}
