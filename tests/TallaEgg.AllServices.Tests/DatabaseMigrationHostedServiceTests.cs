using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.Startup;

namespace TallaEgg.AllServices.Tests;

// The retry half of issue #230: what the migration host does when the database is not answering
// yet. Constructed through the internal constructor so the real backoff — five seconds growing to
// sixty, ten attempts — does not have to be waited out; the schedule itself is a constant and is
// not what these assert.
public class DatabaseMigrationHostedServiceTests
{
    private static readonly TimeSpan NoDelay = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task ExecuteAsync_MigrationSucceedsImmediately_OpensReadinessAndLeavesTheHostRunning()
    {
        var readiness = new DatabaseReadiness();
        var lifetime = new RecordingHostApplicationLifetime();
        var attempts = 0;

        var service = Create(readiness, lifetime, (_, _) =>
        {
            attempts++;
            return Task.CompletedTask;
        });

        await RunToCompletionAsync(service);

        Assert.Equal(1, attempts);
        Assert.True(readiness.IsReady);
        Assert.False(lifetime.StopRequested);
    }

    [Fact]
    public async Task ExecuteAsync_DatabaseArrivesLate_RetriesUntilItSucceeds()
    {
        var readiness = new DatabaseReadiness();
        var lifetime = new RecordingHostApplicationLifetime();
        var attempts = 0;

        var service = Create(readiness, lifetime, (_, _) =>
        {
            attempts++;

            // Stands in for a SQL Server that is Running but not yet accepting connections.
            if (attempts < 3)
            {
                throw new InvalidOperationException("the database is not answering yet");
            }

            return Task.CompletedTask;
        });

        await RunToCompletionAsync(service);

        Assert.Equal(3, attempts);
        Assert.True(readiness.IsReady);

        // The recovery is what matters: no restart was needed, and the host was never stopped.
        Assert.False(lifetime.StopRequested);
    }

    [Fact]
    public async Task ExecuteAsync_MigrationNeverSucceeds_StopsTheHostAndLeavesReadinessClosed()
    {
        var readiness = new DatabaseReadiness();
        var lifetime = new RecordingHostApplicationLifetime();
        var attempts = 0;

        var service = Create(readiness, lifetime, (_, _) => throw new InvalidOperationException($"attempt {++attempts} failed"));

        // The service under test sets the process exit code so a supervisor can tell an abandoned
        // migration from a clean stop. Left set, it would be the exit code of this test run.
        var exitCodeBeforeTest = Environment.ExitCode;
        try
        {
            await RunToCompletionAsync(service);

            Assert.Equal(MaxAttemptsUnderTest, attempts);
            Assert.NotEqual(0, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = exitCodeBeforeTest;
        }

        // A database that never comes up has to end in a stopped service, not one that reports
        // Running and answers 503 to everything for the rest of the day.
        Assert.True(lifetime.StopRequested);
        Assert.False(readiness.IsReady);
    }

    [Fact]
    public async Task ExecuteAsync_HostShutsDownWhileRetrying_StopsWithoutStoppingTheHostAgain()
    {
        var readiness = new DatabaseReadiness();
        var lifetime = new RecordingHostApplicationLifetime();

        // Long enough that the loop is certainly waiting out its backoff when the host stops,
        // rather than racing to exhaust the budget first.
        var service = Create(
            readiness,
            lifetime,
            (_, _) => throw new InvalidOperationException("the database is not answering yet"),
            retryDelay: TimeSpan.FromMinutes(1));

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.False(readiness.IsReady);

        // Shutting down is not the same as giving up: the retry budget was never exhausted, so
        // nothing here should look like a failed migration.
        Assert.False(lifetime.StopRequested);
    }

    private const int MaxAttemptsUnderTest = 3;

    private static DatabaseMigrationHostedService Create(
        DatabaseReadiness readiness,
        IHostApplicationLifetime lifetime,
        DatabaseMigrationStep migrate,
        TimeSpan? retryDelay = null)
    {
        var scopeFactory = new ServiceCollection()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        return new DatabaseMigrationHostedService(
            scopeFactory,
            migrate,
            readiness,
            lifetime,
            NullLogger<DatabaseMigrationHostedService>.Instance,
            MaxAttemptsUnderTest,
            retryDelay ?? NoDelay,
            retryDelay ?? NoDelay);
    }

    private static async Task RunToCompletionAsync(DatabaseMigrationHostedService service)
    {
        await service.StartAsync(CancellationToken.None);

        // StartAsync returns as soon as ExecuteAsync yields, which is the behaviour the fix
        // depends on; ExecuteTask is the loop that carries on behind it.
        await service.ExecuteTask!;
    }

    private sealed class RecordingHostApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopRequested = true;
    }
}
