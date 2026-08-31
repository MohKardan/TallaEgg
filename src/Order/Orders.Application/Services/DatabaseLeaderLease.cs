using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure;

namespace Orders.Application.Services;

/// <summary>
/// Leader election over the Orders database (issue #160).
///
/// The database is used because it is the only thing every instance can see. An in-process lock
/// such as the <c>SemaphoreSlim</c> in <see cref="MatchingEngineService"/> serialises a loop
/// against itself and is invisible to a second process, which is precisely how the constraint
/// this replaces could be broken without anyone noticing.
///
/// <para>
/// Every claim is one statement whose conditions live in its WHERE clause. That is what makes it
/// safe: reading "the lease is free" and then writing "the lease is mine" as two operations lets
/// two instances both read free before either writes, which is the same race the plain SELECT in
/// the outbox processor had.
/// </para>
///
/// <para>
/// Times come from <see cref="DateTime.UtcNow"/> on the application server, consistent with the
/// rest of the outbox scheduling. Instances therefore have to agree roughly on the time; two
/// hosts with clocks minutes apart would hand the same role to both.
/// </para>
///
/// Registered as a singleton and opens its own scope per call, because the callers are singletons
/// (a hosted service cannot depend on a scoped <c>DbContext</c>) — the same reason
/// <see cref="MatchingEngineService"/> takes an <see cref="IServiceScopeFactory"/>.
/// </summary>
public class DatabaseLeaderLease : ILeaderLease
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InstanceIdentity _identity;
    private readonly ILogger<DatabaseLeaderLease> _logger;

    public DatabaseLeaderLease(
        IServiceScopeFactory scopeFactory,
        InstanceIdentity identity,
        ILogger<DatabaseLeaderLease> logger)
    {
        _scopeFactory = scopeFactory;
        _identity = identity;
        _logger = logger;
    }

    public async Task<LeaderLeaseResult> TryAcquireOrRenewAsync(
        string role, TimeSpan duration, CancellationToken ct = default)
    {
        var owner = _identity.Value;
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(duration);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        // 1. Renewal — the role is ours and has not run out. AcquiredAt is left alone so it keeps
        //    recording when this instance first took over, which is what tells an operator whether
        //    leadership is stable or flapping between two hosts.
        var renewed = await db.ServiceLeases
            .Where(l => l.Role == role && l.Owner == owner && l.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, expiresAt), ct);

        if (renewed > 0) return LeaderLeaseResult.Leader;

        // 2. Takeover — nobody holds it any more. This also covers our own expired lease: an
        //    instance that stalled past its expiry has genuinely lost the role and comes back
        //    through the same door as everyone else.
        var takenOver = await db.ServiceLeases
            .Where(l => l.Role == role && l.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Owner, owner)
                .SetProperty(l => l.AcquiredAt, now)
                .SetProperty(l => l.ExpiresAt, expiresAt), ct);

        if (takenOver > 0) return LeaderLeaseResult.Leader;

        // 3. Neither: either somebody else holds a live lease, or this role has never been claimed
        //    and has no row yet.
        var current = await db.ServiceLeases.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Role == role, ct);

        if (current is not null) return LeaderLeaseResult.FollowerOf(current.Owner);

        return await TryCreateAsync(db, role, owner, expiresAt, ct);
    }

    /// <summary>
    /// First claim of a role that has no row yet. Two instances starting together both reach here,
    /// and the primary key on Role settles it: one insert succeeds, the other is refused. Losing
    /// that race is a normal outcome, not a fault — it means the other instance is the leader.
    /// </summary>
    private async Task<LeaderLeaseResult> TryCreateAsync(
        OrdersDbContext db, string role, string owner, DateTime expiresAt, CancellationToken ct)
    {
        try
        {
            db.ServiceLeases.Add(ServiceLease.CreateHeldBy(role, owner, expiresAt));
            await db.SaveChangesAsync(ct);
            return LeaderLeaseResult.Leader;
        }
        catch (DbUpdateException)
        {
            // Read back who won, so the caller can name them. A second failure here leaves the
            // holder unknown, which only costs a less specific log line.
            var winner = await db.ServiceLeases.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Role == role, ct);

            return LeaderLeaseResult.FollowerOf(winner?.Owner);
        }
    }

    public async Task ReleaseAsync(string role, CancellationToken ct = default)
    {
        try
        {
            var owner = _identity.Value;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

            // Expired rather than deleted: the row keeps saying who held the role last, and the
            // Owner check means a shutting-down instance can never release a lease that has
            // already been taken over by someone still running.
            await db.ServiceLeases
                .Where(l => l.Role == role && l.Owner == owner)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, DateTime.UtcNow), ct);
        }
        catch (Exception ex)
        {
            // Swallowed by contract. The lease expires on its own; failing to hand it back early
            // costs one lease period of nobody running the loop, and throwing here during
            // shutdown would obscure whatever is actually stopping the host.
            _logger.LogWarning(ex, "Could not release the {Role} lease on shutdown; it will expire instead.", role);
        }
    }
}
