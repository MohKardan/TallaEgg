using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TallaEgg.Core.Startup;

namespace TallaEgg.AllServices.Tests;

// The startup half of issue #230, wired the way Wallet.Api, Users.Api and Orders.Api wire it:
// AddDatabaseMigrationAtStartup on the services, UseDatabaseReadinessGate on the application.
// Against a bare TestServer rather than any service's Program, because none of them can boot
// here without a live SQL Server (issue #68).
//
// Every test here starts the host while the migration is still blocked, which is the condition
// the issue is about. Move the migration back onto the pre-start path and all four fail.
public class DatabaseMigrationStartupTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StartAsync_MigrationHasNotFinished_HostStillReportsStarted()
    {
        var migrationGate = NewGate();
        using var host = BuildHost((_, _) => migrationGate.Task);

        try
        {
            // The assertion is inside the helper: reaching this line at all is the result.
            await StartWithinTimeoutAsync(host);
        }
        finally
        {
            migrationGate.TrySetResult();
            await StopQuietlyAsync(host);
        }
    }

    [Fact]
    public async Task Request_MigrationHasNotSucceeded_IsRefusedWith503()
    {
        var migrationGate = NewGate();
        using var host = BuildHost((_, _) => migrationGate.Task);

        try
        {
            await StartWithinTimeoutAsync(host);

            var response = await host.GetTestClient().GetAsync("/api/wallet/balance");

            // Serving here would answer against a schema that has not been migrated.
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            migrationGate.TrySetResult();
            await StopQuietlyAsync(host);
        }
    }

    [Fact]
    public async Task Request_DatabaseArrivesLate_IsServedOnceTheMigrationSucceeds()
    {
        var migrationGate = NewGate();
        using var host = BuildHost((_, _) => migrationGate.Task);

        try
        {
            await StartWithinTimeoutAsync(host);

            var client = host.GetTestClient();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/wallet/balance")).StatusCode);

            // The database turns up. No restart and no second process — this host recovers.
            migrationGate.TrySetResult();
            await WaitUntilReadyAsync(host);

            var response = await client.GetAsync("/api/wallet/balance");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("served", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            migrationGate.TrySetResult();
            await StopQuietlyAsync(host);
        }
    }

    [Fact]
    public async Task VersionRequest_MigrationHasNotSucceeded_IsStillAnswered()
    {
        var migrationGate = NewGate();
        using var host = BuildHost((_, _) => migrationGate.Task);

        try
        {
            await StartWithinTimeoutAsync(host);

            // "Which build is running?" is the question a degraded service exists to answer
            // (issue #218), and it reads nothing from the database.
            var response = await host.GetTestClient().GetAsync("/version");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            migrationGate.TrySetResult();
            await StopQuietlyAsync(host);
        }
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static IHost BuildHost(DatabaseMigrationStep migrate) =>
        new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => services.AddDatabaseMigrationAtStartup(migrate))
                    .Configure(app =>
                    {
                        app.UseDatabaseReadinessGate();
                        app.Run(context => context.Response.WriteAsync("served"));
                    });
            })
            .Build();

    /// <summary>
    /// Starts the host and fails if it does not report started while the migration is still
    /// blocked — the regression this whole issue is about.
    /// </summary>
    private static async Task StartWithinTimeoutAsync(IHost host)
    {
        // Started on a pool thread on purpose. A migration back on the pre-start path can block
        // its caller's thread outright rather than hand back a pending task — that is exactly
        // what the old top-level `await MigrateAsync()` did — and this has to fail on that
        // rather than hang with it.
        var start = Task.Run(() => host.StartAsync());
        var first = await Task.WhenAny(start, Task.Delay(StartTimeout));

        Assert.True(
            ReferenceEquals(first, start),
            "The host did not report started while the migration was still blocked, so the migration is back on the pre-start path. " +
            "Under UseWindowsService() that is the window the SCM counts against its start timeout (issue #230).");

        await start;
    }

    private static async Task WaitUntilReadyAsync(IHost host)
    {
        var readiness = host.Services.GetRequiredService<DatabaseReadiness>();
        var deadline = DateTime.UtcNow + ReadyTimeout;

        while (!readiness.IsReady && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(readiness.IsReady, "The migration completed but the readiness gate never opened.");
    }

    /// <summary>
    /// Cleanup only. A host whose start never completed cannot be stopped cleanly, and the
    /// failure that caused it is the one worth reporting.
    /// </summary>
    private static async Task StopQuietlyAsync(IHost host)
    {
        try
        {
            await host.StopAsync();
        }
        catch (Exception)
        {
            // Deliberately ignored; see above.
        }
    }
}
