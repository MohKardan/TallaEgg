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
/// How the two timer-driven background services use leader election (issue #160), and — the part
/// that is easy to get wrong — what it must NOT gate.
///
/// The gate is asked through each service's <c>TryLeadAsync</c> rather than by starting the loops
/// and waiting, for the same reason <c>PublishIfDueAsync</c> is internal: a test that depends on a
/// one-second timer proves less and fails at random.
/// </summary>
public class BackgroundLeaderGateTests : IDisposable
{
    private readonly SqliteConnection _connection;

    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal Price = 16_967_542.36m;
    private const decimal Quantity = 2m;

    public BackgroundLeaderGateTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    // ── The matching engine's background sweep ──────────────────────────────────

    [Fact]
    public async Task TheMatchingSweep_RunsWhenThisInstanceHoldsTheLease()
    {
        using var provider = BuildMatchingProvider(new AlwaysLeaderLease());
        var engine = provider.GetRequiredService<MatchingEngineService>();

        Assert.True(await engine.TryLeadAsync());
    }

    [Fact]
    public async Task TheMatchingSweep_StandsDownWhenAnotherInstanceHoldsTheLease()
    {
        using var provider = BuildMatchingProvider(new HeldElsewhereLease());
        var engine = provider.GetRequiredService<MatchingEngineService>();

        Assert.False(await engine.TryLeadAsync());
    }

    /// <summary>
    /// The one thing the gate must never touch. <c>OrderService</c> calls the engine on the
    /// request path, so gating that would mean an order placed against a follower instance was
    /// never matched at all — trading would silently half-work on every instance but one.
    ///
    /// <para>
    /// Asserted by whether the order book is consulted, not by whether a trade appears, for the
    /// reason <see cref="DealerModeSkipsBackgroundMatchingTests"/> already records: the order-book
    /// query orders by <c>Price</c>, and SQLite rejects a decimal in ORDER BY. Production runs on
    /// SQL Server. "The engine reached the order book" is the part the gate decides anyway.
    /// </para>
    /// </summary>
    [Fact]
    public async Task OnAFollowerInstance_TheRequestPathStillReachesTheOrderBook()
    {
        var log = new CapturingLoggerProvider();
        using var provider = BuildMatchingProvider(new HeldElsewhereLease(), log);
        var engine = provider.GetRequiredService<MatchingEngineService>();

        var (buyId, _) = SeedMatchablePair();

        Assert.False(await engine.TryLeadAsync()); // this instance does not run the sweep

        // The overload OrderService calls when an order is placed.
        await engine.ProcessOrderAsync(buyId);

        Assert.Contains(log.Messages, m => m.Contains("sell orders for asset"));
    }

    // ── The auto-quote publisher ────────────────────────────────────────────────

    [Fact]
    public async Task TheQuotePublisher_PublishesWhenThisInstanceHoldsTheLease()
    {
        using var provider = BuildPublisherProvider();
        var publisher = new AutoQuotePublisherService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoQuotePublisherService>.Instance,
            new AlwaysLeaderLease());

        Assert.True(await publisher.TryLeadAsync());
    }

    [Fact]
    public async Task TheQuotePublisher_StandsDownWhenAnotherInstanceHoldsTheLease()
    {
        using var provider = BuildPublisherProvider();
        var publisher = new AutoQuotePublisherService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoQuotePublisherService>.Instance,
            new HeldElsewhereLease());

        Assert.False(await publisher.TryLeadAsync());
    }

    // ── Noticing the second instance ────────────────────────────────────────────

    /// <summary>
    /// The acceptance criterion of the issue: it must be impossible to deploy a second instance
    /// without noticing. The warning naming the other instance is how that happens, so it is
    /// asserted rather than left as a hopeful log line.
    /// </summary>
    [Fact]
    public async Task AFollower_WarnsOnceAndNamesTheInstanceHoldingTheLease()
    {
        var logger = new CapturingLogger<BackgroundLeaderGateTests>();
        var gate = new LeaderGate(
            ServiceLeaseRoles.MatchingEngine,
            TimeSpan.FromSeconds(30),
            new HeldElsewhereLease("the-other-host"),
            logger);

        Assert.False(await gate.TryLeadAsync());

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("the-other-host", warning.Message);
    }

    /// <summary>
    /// A follower re-checks for as long as it runs. Saying so every time would bury the transition
    /// that matters under a line every few seconds, so only changes are reported.
    /// </summary>
    [Fact]
    public async Task AFollowerThatKeepsChecking_DoesNotRepeatTheWarning()
    {
        var logger = new CapturingLogger<BackgroundLeaderGateTests>();

        // A lease of zero would be rejected, so the shortest usable one is used and the gate is
        // asked twice with the renewal interval already elapsed.
        var gate = new LeaderGate(
            ServiceLeaseRoles.MatchingEngine,
            TimeSpan.FromTicks(1),
            new HeldElsewhereLease("the-other-host"),
            logger);

        Assert.False(await gate.TryLeadAsync());
        Assert.False(await gate.TryLeadAsync());
        Assert.False(await gate.TryLeadAsync());

        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>
    /// A lease that cannot be reached leaves this instance standing down rather than acting on a
    /// claim it could not confirm — and, critically, does not throw. The sweep's error handling
    /// sits outside its while loop, so an exception here would end background matching for the
    /// life of the process rather than skipping one tick.
    /// </summary>
    [Fact]
    public async Task WhenTheLeaseCannotBeReached_TheGateStandsDownWithoutThrowing()
    {
        var logger = new CapturingLogger<BackgroundLeaderGateTests>();
        var gate = new LeaderGate(
            ServiceLeaseRoles.MatchingEngine,
            TimeSpan.FromSeconds(30),
            new ThrowingLease(),
            logger);

        Assert.False(await gate.TryLeadAsync());
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Exception is not null);
    }

    private sealed class ThrowingLease : ILeaderLease
    {
        public Task<LeaderLeaseResult> TryAcquireOrRenewAsync(string role, TimeSpan duration, CancellationToken ct = default) =>
            throw new InvalidOperationException("database unreachable");

        public Task ReleaseAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
    }

    // ── Wiring ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Goes through the production <c>AddMatchingEngine</c>, with the lease swapped for a double
    /// so the test decides whether this instance leads.
    /// </summary>
    private ServiceProvider BuildMatchingProvider(ILeaderLease lease, ILoggerProvider? log = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());

        if (log is null)
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        else
            services.AddLogging(b => b.AddProvider(log));

        services.AddScoped(_ => NewContext());
        services.AddScoped<OrderMatchingRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<MarketModeProvider>();
        services.AddScoped<IWalletApiClient, StubWalletApiClient>();
        services.AddSingleton(lease);
        services.AddMatchingEngine();

        return services.BuildServiceProvider();
    }

    private ServiceProvider BuildPublisherProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped(_ => NewContext());

        return services.BuildServiceProvider();
    }

    private (Guid BuyId, Guid SellId) SeedMatchablePair()
    {
        using var db = NewContext();

        var buy = Order.CreateMakerOrder(Symbol, Quantity, Price, Guid.NewGuid(), OrderSide.Buy, TradingType.Spot);
        var sell = Order.CreateMakerOrder(Symbol, Quantity, Price, Guid.NewGuid(), OrderSide.Sell, TradingType.Spot);
        buy.Confirm();
        sell.Confirm();

        db.Orders.AddRange(buy, sell);
        db.SaveChanges();

        return (buy.Id, sell.Id);
    }

    /// <summary>
    /// Collects log messages so a test can tell whether the engine reached the order book. A
    /// private copy rather than a shared one, because the only other user is a test that owns its
    /// own and changing that file is not part of this issue.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = new();

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
