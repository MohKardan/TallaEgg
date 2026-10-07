using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Issue #280: the order-book best bid/ask read every order ever placed for the symbol — filled,
/// cancelled, years old — filtered it in memory, and logged the open set as indented JSON.
///
/// <para>
/// Only an <c>OrderBook</c>-mode symbol reaches that read; a dealer symbol answers from its quote and
/// returns first. No symbol runs in OrderBook mode today, so the cost was latent. It is not dead code:
/// <c>docs/product/DIRECTION.md</c> records that customer-to-customer orders may return, and this is
/// the read they would use.
/// </para>
/// </summary>
public class BestBidAskReadTests : IDisposable
{
    private const string Gold = CurrenciesConstant.MAUA_IRT;
    private const string Coin = CurrenciesConstant.SEKE_BAHAR_IRT;

    // Realistic per-gram gold prices around the 2026-09 level.
    private const decimal OpenBestBid = 17_000_000m;
    private const decimal OpenBestAsk = 17_250_000m;

    private readonly SqliteConnection _connection;
    private readonly CommandCapture _commands = new();
    private readonly List<OrdersDbContext> _contexts = [];

    public BestBidAskReadTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
            context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Behaviour the change must keep: only open orders of the symbol set the price. The closed
    /// orders are priced to win if they were counted — a filled buy above the open best bid, a
    /// cancelled sell below the open best ask — and the other symbol's open orders would win both.
    /// </summary>
    /// <remarks>Green before the fix too; it pins the answer while the read changes underneath it.</remarks>
    [Fact]
    public async Task GetBestBidAskAsync_PricesOnlyFromTheSymbolsOpenOrders()
    {
        await SeedBookAsync();

        var prices = await BuildOrderService().GetBestBidAskAsync(Gold, TradingType.Spot);

        Assert.Equal(OpenBestBid, prices.BestBidPrice);
        Assert.Equal(OpenBestAsk, prices.BestAskPrice);
    }

    /// <summary>
    /// The defect itself: closed orders must be filtered by the database, not loaded and discarded.
    /// Asserted on the SQL that reached the database, because the answer above is the same either way.
    /// </summary>
    [Fact]
    public async Task GetBestBidAskAsync_AsksTheDatabaseForOpenOrdersOnly()
    {
        await SeedBookAsync();
        _commands.Texts.Clear();

        await BuildOrderService().GetBestBidAskAsync(Gold, TradingType.Spot);

        // Only the WHERE clause counts: "Status" is in every SELECT list, so matching the whole text
        // would pass against the unfiltered read too — this assertion did exactly that once.
        var orderFilters = _commands.Texts
            .Where(t => t.Contains("FROM \"Orders\""))
            .Select(t => t.Contains("WHERE") ? t[t.IndexOf("WHERE", StringComparison.Ordinal)..] : "")
            .ToList();
        Assert.NotEmpty(orderFilters);
        Assert.All(orderFilters, where => Assert.Contains("\"Status\"", where));
    }

    /// <summary>
    /// The database filter restates <see cref="Order.IsActive"/> in a form SQL can run, so the two can
    /// drift. One order in every status, and the read must return exactly those the entity calls active.
    /// </summary>
    [Fact]
    public async Task GetActiveOrdersByAssetAsync_ReturnsExactlyTheOrdersTheEntityCallsActive()
    {
        var idsByStatus = new Dictionary<OrderStatus, Guid>();
        foreach (var status in Enum.GetValues<OrderStatus>())
            idsByStatus[status] = await AddOrderAsync(Gold, OrderSide.Buy, OpenBestBid, status);

        await using var db = NewContext();
        var expected = (await db.Orders.AsNoTracking().ToListAsync())
            .Where(o => o.IsActive()).Select(o => o.Id).OrderBy(id => id).ToList();

        var actual = (await new OrderRepository(Tracked(NewContext()), NullLogger<OrderRepository>.Instance)
                .GetActiveOrdersByAssetAsync(Gold, TradingType.Spot))
            .Select(o => o.Id).OrderBy(id => id).ToList();

        Assert.Equal(expected, actual);

        // Guards the comparison against being vacuous: if every status, or none, were active, the
        // equality above would hold without the filter excluding anything.
        Assert.NotEmpty(actual);
        Assert.NotEqual(idsByStatus.Count, actual.Count);
    }

    // ── setup ───────────────────────────────────────────────────────────────────

    private async Task SeedBookAsync()
    {
        // The open book.
        await AddOrderAsync(Gold, OrderSide.Buy, 16_900_000m, OrderStatus.Confirmed);
        await AddOrderAsync(Gold, OrderSide.Buy, OpenBestBid, OrderStatus.Partially);
        await AddOrderAsync(Gold, OrderSide.Sell, OpenBestAsk, OrderStatus.Confirmed);
        await AddOrderAsync(Gold, OrderSide.Sell, 17_400_000m, OrderStatus.Pending);

        // History that would win if it were counted.
        await AddOrderAsync(Gold, OrderSide.Buy, 17_100_000m, OrderStatus.Completed);
        await AddOrderAsync(Gold, OrderSide.Sell, 17_200_000m, OrderStatus.Cancelled);

        // Another symbol's open book, priced to win both sides.
        await AddOrderAsync(Coin, OrderSide.Buy, 97_500_000m, OrderStatus.Confirmed);
        await AddOrderAsync(Coin, OrderSide.Sell, 1m, OrderStatus.Confirmed);
    }

    private async Task<Guid> AddOrderAsync(string symbol, OrderSide side, decimal price, OrderStatus status)
    {
        await using var db = NewContext();
        var order = Order.CreateMakerOrder(symbol, 1m, price, Guid.NewGuid(), side, OrderType.Limit, TradingType.Spot);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await db.Orders.Where(o => o.Id == order.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status));

        return order.Id;
    }

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_commands)
            .Options);

    private OrdersDbContext Tracked(OrdersDbContext context)
    {
        _contexts.Add(context);
        return context;
    }

    private OrderService BuildOrderService()
    {
        var context = Tracked(NewContext());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:MarketModes:" + Gold] = "OrderBook",
                ["Matching:MarketModes:" + Coin] = "OrderBook",
                ["UsersApiUrl"] = "http://users.test/api"
            })
            .Build();

        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);
        var wallet = new StubWalletApiClient();

        return new OrderService(
            orderRepository, wallet, new NoOpMatchingEngine(),
            NullLogger<OrderService>.Instance,
            new UsersApiClient(new HttpClient(), configuration, NullLogger<UsersApiClient>.Instance),
            new OrderCollateralReconciler(orderRepository, new TradeRepository(context), wallet,
                NullLogger<OrderCollateralReconciler>.Instance),
            new QuoteRepository(context, NullLogger<QuoteRepository>.Instance),
            new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance));
    }

    private sealed class NoOpMatchingEngine : IMatchingEngine
    {
        public Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Every SQL command text executed, in order.</summary>
    private sealed class CommandCapture : DbCommandInterceptor
    {
        public List<string> Texts { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Texts.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
