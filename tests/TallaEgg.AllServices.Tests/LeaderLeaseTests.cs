using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Leader election for the background loops that must have exactly one writer (issue #160).
///
/// The outbox can be coordinated per message, because each unit of work is a row. The order-book
/// sweep and the quote publisher cannot: both are timers with nothing to claim, so what they claim
/// is the role. Two instances publishing quotes on the same tick would write two quote rows per
/// symbol and, when a price falls outside the plausibility band, put the same approval request in
/// front of the admin twice.
///
/// The real <see cref="DatabaseLeaderLease"/> is exercised against SQLite here rather than a
/// double, because the whole mechanism is the atomicity of its statements — a stub would prove
/// nothing about it.
/// </summary>
public class LeaderLeaseTests : IDisposable
{
    private const string Role = ServiceLeaseRoles.MatchingEngine;
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public LeaderLeaseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var setup = new OrdersDbContext(Options()))
            setup.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddScoped(_ => new OrdersDbContext(Options()));
        _provider = services.BuildServiceProvider();
    }

    private DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private DatabaseLeaderLease LeaseFor(string instance) => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        new InstanceIdentity(instance),
        NullLogger<DatabaseLeaderLease>.Instance);

    /// <summary>
    /// The first claim of a role that has never been held. Both instances reach the insert, and
    /// the primary key on Role decides it — losing is a normal answer, not an error.
    /// </summary>
    [Fact]
    public async Task WhenTwoInstancesClaimAnUnheldRole_OnlyOneBecomesLeader()
    {
        var a = await LeaseFor("instance-a").TryAcquireOrRenewAsync(Role, Lease);
        var b = await LeaseFor("instance-b").TryAcquireOrRenewAsync(Role, Lease);

        Assert.True(a.IsLeader);
        Assert.False(b.IsLeader);
    }

    /// <summary>
    /// The follower is told who holds the role. That name is what turns "this instance is idle"
    /// in the log into "a second Orders.Api is running", which is the point of the whole issue:
    /// deploying a second instance must not be silent.
    /// </summary>
    [Fact]
    public async Task AFollower_IsToldWhichInstanceHoldsTheRole()
    {
        await LeaseFor("instance-a").TryAcquireOrRenewAsync(Role, Lease);

        var b = await LeaseFor("instance-b").TryAcquireOrRenewAsync(Role, Lease);

        Assert.False(b.IsLeader);
        Assert.Equal("instance-a", b.Holder);
    }

    /// <summary>The holder keeps the role by asking again; renewing is not a takeover.</summary>
    [Fact]
    public async Task TheHolder_KeepsTheRoleOnRenewal()
    {
        var lease = LeaseFor("instance-a");

        Assert.True((await lease.TryAcquireOrRenewAsync(Role, Lease)).IsLeader);
        Assert.True((await lease.TryAcquireOrRenewAsync(Role, Lease)).IsLeader);

        // AcquiredAt still records the original takeover, so a flapping leader is visible.
        var row = await SingleLeaseAsync();
        Assert.Equal("instance-a", row.Owner);
        Assert.True(row.ExpiresAt > row.AcquiredAt);
    }

    /// <summary>
    /// An instance that dies holding the role must not take the loop down with it. Nothing runs
    /// the sweep until the lease runs out, and then someone does.
    /// </summary>
    [Fact]
    public async Task WhenTheHoldersLeaseExpires_AnotherInstanceTakesOver()
    {
        await LeaseFor("instance-a").TryAcquireOrRenewAsync(Role, Lease);
        ExpireTheLease();

        var b = await LeaseFor("instance-b").TryAcquireOrRenewAsync(Role, Lease);

        Assert.True(b.IsLeader);
        Assert.Equal("instance-b", (await SingleLeaseAsync()).Owner);
    }

    /// <summary>
    /// A graceful shutdown hands the role back rather than leaving the next instance to wait out
    /// the lease — half a minute of nobody sweeping the order book, for no reason.
    /// </summary>
    [Fact]
    public async Task AfterTheHolderReleases_AnotherInstanceTakesOverImmediately()
    {
        var a = LeaseFor("instance-a");
        await a.TryAcquireOrRenewAsync(Role, Lease);

        await a.ReleaseAsync(Role);

        Assert.True((await LeaseFor("instance-b").TryAcquireOrRenewAsync(Role, Lease)).IsLeader);
    }

    /// <summary>
    /// Releasing only affects a role this instance still holds. A slow shutdown must not hand away
    /// a lease that has already expired and been taken over by an instance that is running.
    /// </summary>
    [Fact]
    public async Task ReleasingARoleTakenOverByAnotherInstance_DoesNothing()
    {
        var a = LeaseFor("instance-a");
        await a.TryAcquireOrRenewAsync(Role, Lease);

        ExpireTheLease();
        await LeaseFor("instance-b").TryAcquireOrRenewAsync(Role, Lease);

        await a.ReleaseAsync(Role);

        // B still holds a live lease, so a third instance cannot step in.
        var c = await LeaseFor("instance-c").TryAcquireOrRenewAsync(Role, Lease);
        Assert.False(c.IsLeader);
        Assert.Equal("instance-b", c.Holder);
    }

    /// <summary>Roles are independent: holding one says nothing about the other.</summary>
    [Fact]
    public async Task DifferentRoles_AreHeldSeparately()
    {
        await LeaseFor("instance-a").TryAcquireOrRenewAsync(ServiceLeaseRoles.MatchingEngine, Lease);

        var b = await LeaseFor("instance-b")
            .TryAcquireOrRenewAsync(ServiceLeaseRoles.AutoQuotePublisher, Lease);

        Assert.True(b.IsLeader);
    }

    private async Task<ServiceLease> SingleLeaseAsync()
    {
        using var db = new OrdersDbContext(Options());
        return await db.ServiceLeases.AsNoTracking().SingleAsync(l => l.Role == Role);
    }

    /// <summary>Ages the lease out, standing in for an instance that stopped renewing it.</summary>
    private void ExpireTheLease()
    {
        using var db = new OrdersDbContext(Options());
        db.ServiceLeases
            .Where(l => l.Role == Role)
            .ExecuteUpdate(s => s.SetProperty(l => l.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
    }
}
