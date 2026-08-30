using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// In Dealer mode the background matching loop must not match anything (issue #74).
///
/// A quote acceptance creates both orders, matches them in full and consumes them inside one
/// operation, so there is never resting liquidity for the loop to pair. Its only possible
/// effect on a dealer symbol is to reach the pair a fill is already matching — which is what
/// produced two trades from one order pair and charged the customer twice.
///
/// The semaphore from #53 serialises this loop against itself. It does not cover
/// <c>QuoteFillService</c>, which calls the repository directly. Removing the second matcher
/// is smaller and surer than coordinating two.
///
/// Both directions are asserted. A guard that also stopped order-book matching would replace
/// one defect with a worse one: no trades at all when peer-to-peer trading opens.
/// </summary>
public class DealerModeSkipsBackgroundMatchingTests : IDisposable
{
    private readonly SqliteConnection _connection;

    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal Price = 16_967_542.36m;
    private const decimal Quantity = 2m;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    public DealerModeSkipsBackgroundMatchingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    /// <summary>
    /// Calls the production <c>AddMatchingEngine</c> so this covers the real wiring, with the
    /// database and the wallet swapped for local doubles.
    /// </summary>
    private ServiceProvider BuildProvider(params (string Key, string Value)[] settings) =>
        BuildProvider(null, settings);

    private ServiceProvider BuildProvider(
        ILoggerProvider? logProvider,
        params (string Key, string Value)[] settings)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build());

        if (logProvider is null)
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        else
            services.AddLogging(b => b.AddProvider(logProvider));
        services.AddScoped(_ => NewContext());
        services.AddScoped<OrderMatchingRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<MarketModeProvider>();
        services.AddScoped<IWalletApiClient, StubWalletApiClient>();
        services.AddMatchingEngine();

        return services.BuildServiceProvider();
    }

    /// <summary>A confirmed, unmatched pair — what the loop would pick up.</summary>
    private (Guid BuyId, Guid SellId) SeedMatchablePair()
    {
        using var db = NewContext();

        var buy = Order.CreateMakerOrder(Symbol, Quantity, Price, _buyerId, OrderSide.Buy, TradingType.Spot);
        var sell = Order.CreateMakerOrder(Symbol, Quantity, Price, _sellerId, OrderSide.Sell, TradingType.Spot);
        buy.Confirm();
        sell.Confirm();

        db.Orders.AddRange(buy, sell);
        db.SaveChanges();

        return (buy.Id, sell.Id);
    }

    private async Task<int> TradeCountAsync()
    {
        await using var db = NewContext();
        return await db.Trades.CountAsync();
    }

    /// <summary>
    /// Note: this pair of "creates no trade" tests would also pass without the guard, because
    /// the order-book query cannot run under SQLite at all (see
    /// <see cref="InOrderBookMode_TheOrderBookIsConsulted"/>). They state the outcome that
    /// matters; <see cref="InDealerMode_TheOrderBookIsNeverConsulted"/> is the one that
    /// actually fails when the guard is removed — confirmed by removing it.
    /// </summary>
    [Fact]
    public async Task InDealerMode_TheBackgroundLoopCreatesNoTrade()
    {
        var (buyId, _) = SeedMatchablePair();

        using var provider = BuildProvider(
            ("Matching:MarketModes:" + Symbol, "Dealer"),
            ("Matching:MarketMakerUserId", _sellerId.ToString()));

        var engine = provider.GetRequiredService<MatchingEngineService>();

        var matched = await engine.ProcessOrderForMatchingAsync(buyId);

        Assert.False(matched, "a dealer symbol must not be matched by the background loop");
        Assert.Equal(0, await TradeCountAsync());
    }

    [Fact]
    public async Task InDealerMode_TheWholeSweepCreatesNoTrade()
    {
        SeedMatchablePair();

        using var provider = BuildProvider(
            ("Matching:MarketModes:" + Symbol, "Dealer"),
            ("Matching:MarketMakerUserId", _sellerId.ToString()));

        var engine = provider.GetRequiredService<MatchingEngineService>();

        await engine.ProcessAllPendingOrdersAsync(CancellationToken.None);

        Assert.Equal(0, await TradeCountAsync());
    }

    // ── the other direction: the guard must not over-block ──────────────────────

    /// <summary>
    /// Asserted by whether the order book is consulted at all, rather than by whether a trade
    /// results.
    ///
    /// <para>
    /// <b>Why:</b> the order-book query cannot run under SQLite —
    /// <c>GetSellOrdersAsync</c> orders by <c>Price</c>, and SQLite raises
    /// <c>NotSupportedException: SQLite does not support expressions of type 'decimal' in ORDER
    /// BY clauses</c>. Verified directly, not assumed. Production uses SQL Server, where it is
    /// fine. So "a trade appears" is not reachable here, but "the engine reached the order
    /// book" is — and that is the thing the guard decides.
    /// </para>
    ///
    /// <para>
    /// Without this direction, a guard that disabled matching altogether would satisfy the two
    /// tests above and nobody would find out until peer-to-peer trading was switched on.
    /// </para>
    /// </summary>
    [Fact]
    public async Task InOrderBookMode_TheOrderBookIsConsulted()
    {
        var (buyId, _) = SeedMatchablePair();

        var log = new CapturingLoggerProvider();
        using var provider = BuildProvider(log, ("Matching:MarketModes:" + Symbol, "OrderBook"));

        await provider.GetRequiredService<MatchingEngineService>().ProcessOrderForMatchingAsync(buyId);

        Assert.Contains(log.Messages, m => m.Contains("sell orders for asset"));
    }

    /// <summary>In Dealer mode it must not even be consulted — nothing to consult it for.</summary>
    [Fact]
    public async Task InDealerMode_TheOrderBookIsNeverConsulted()
    {
        var (buyId, _) = SeedMatchablePair();

        var log = new CapturingLoggerProvider();
        using var provider = BuildProvider(log,
            ("Matching:MarketModes:" + Symbol, "Dealer"),
            ("Matching:MarketMakerUserId", _sellerId.ToString()));

        await provider.GetRequiredService<MatchingEngineService>().ProcessOrderForMatchingAsync(buyId);

        Assert.DoesNotContain(log.Messages, m => m.Contains("sell orders for asset"));
    }

    /// <summary>
    /// With no configuration the mode falls back to OrderBook, the historical behaviour, so the
    /// book must still be consulted. (That the fallback is silent is its own problem, tracked
    /// in #73; this only pins that the new guard did not change it.)
    /// </summary>
    [Fact]
    public async Task WithNoMarketModeConfigured_TheOrderBookIsStillConsulted()
    {
        var (buyId, _) = SeedMatchablePair();

        var log = new CapturingLoggerProvider();
        using var provider = BuildProvider(log);

        await provider.GetRequiredService<MatchingEngineService>().ProcessOrderForMatchingAsync(buyId);

        Assert.Contains(log.Messages, m => m.Contains("sell orders for asset"));
    }

    /// <summary>Collects log messages so a test can tell which code path ran.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Sink(Messages);

        public void Dispose() { }

        private sealed class Sink(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (messages) messages.Add(formatter(state, exception));
            }
        }
    }
}
