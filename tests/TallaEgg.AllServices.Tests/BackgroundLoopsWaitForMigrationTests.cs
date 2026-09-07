using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core.Startup;

namespace TallaEgg.AllServices.Tests;

// Orders.Api's three background loops used to be guaranteed to start after the migration, because
// the migration ran before app.Run(). Since issue #230 it runs alongside the host, so each loop
// waits on DatabaseReadiness instead. The readiness gate cannot cover them — it turns away HTTP
// requests, and a loop reaches the database without one.
//
// This matters beyond log noise: AutoQuotePublisherService.ExecuteAsync calls ActiveSymbolsAsync
// outside any try/catch, and an exception escaping a BackgroundService stops the entire host under
// the default BackgroundServiceExceptionBehavior.StopHost — the outage #230 exists to remove,
// re-entered through the back door on any deploy that carries a pending migration.
public class BackgroundLoopsWaitForMigrationTests
{
    /// <summary>
    /// Long enough for a loop that does not wait to reach its first query — each of the three
    /// starts working immediately, before its first poll delay.
    /// </summary>
    private static readonly TimeSpan GraceWindow = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task OutboxProcessor_MigrationHasNotSucceeded_DoesNotReachTheDatabase()
    {
        var scopeFactory = new ForbiddenScopeFactory();

        var service = new OutboxProcessorService(
            scopeFactory,
            new InstanceIdentity("waits-for-migration-tests"),
            NullLogger<OutboxProcessorService>.Instance,
            new DatabaseReadiness());

        await RunBrieflyAsync(service);

        Assert.False(scopeFactory.WasUsed, scopeFactory.Explanation);
    }

    [Fact]
    public async Task AutoQuotePublisher_MigrationHasNotSucceeded_DoesNotReachTheDatabase()
    {
        var scopeFactory = new ForbiddenScopeFactory();

        var service = new AutoQuotePublisherService(
            scopeFactory,
            NullLogger<AutoQuotePublisherService>.Instance,
            new AlwaysLeaderLease(),
            new DatabaseReadiness());

        await RunBrieflyAsync(service);

        Assert.False(scopeFactory.WasUsed, scopeFactory.Explanation);
    }

    [Fact]
    public async Task MatchingEngine_MigrationHasNotSucceeded_DoesNotReachTheDatabase()
    {
        var scopeFactory = new ForbiddenScopeFactory();

        var service = new MatchingEngineService(
            scopeFactory,
            NullLogger<MatchingEngineService>.Instance,
            new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().Build(),
            new AlwaysLeaderLease(),
            new DatabaseReadiness());

        await RunBrieflyAsync(service);

        Assert.False(scopeFactory.WasUsed, scopeFactory.Explanation);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_MigrationSucceeds_ReleasesTheWaiter()
    {
        var readiness = new DatabaseReadiness();
        var waiting = readiness.WaitUntilReadyAsync(CancellationToken.None);

        Assert.False(waiting.IsCompleted);

        readiness.MarkReady();

        Assert.True(await waiting);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_HostShutsDownFirst_ReturnsFalseRatherThanThrowing()
    {
        var readiness = new DatabaseReadiness();
        using var shutdown = new CancellationTokenSource();

        var waiting = readiness.WaitUntilReadyAsync(shutdown.Token);
        shutdown.Cancel();

        // A loop that has to catch here is a loop that will one day forget to.
        Assert.False(await waiting);
    }

    private static async Task RunBrieflyAsync(Microsoft.Extensions.Hosting.BackgroundService service)
    {
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(GraceWindow);

        // Rethrows anything the loop threw, so a loop that fell over rather than waiting fails
        // here rather than passing quietly.
        await service.StopAsync(CancellationToken.None);
    }

    private sealed class ForbiddenScopeFactory : IServiceScopeFactory
    {
        public bool WasUsed { get; private set; }

        public string Explanation =>
            "The loop opened a scope before the migration had succeeded, so it would have queried a " +
            "schema that may still be changing (issue #230).";

        public IServiceScope CreateScope()
        {
            WasUsed = true;
            throw new InvalidOperationException(Explanation);
        }
    }
}
