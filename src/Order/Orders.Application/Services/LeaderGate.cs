using Microsoft.Extensions.Logging;

namespace Orders.Application.Services;

/// <summary>
/// One background loop's side of leader election (issue #160): holds the lease for a single role,
/// paces the renewals, and reports the moment this instance gains or loses the role.
///
/// <para>
/// It exists so the two loops that need this do not each grow their own copy of the same three
/// decisions — how often to renew, what to believe between renewals, and what to say when the
/// answer changes. The loops themselves just ask "may I run?" once per tick.
/// </para>
///
/// <para>
/// Renewal happens at half the lease, in both directions. A leader that renews with half its
/// lease still to run cannot be overtaken by its own slowness; a follower that re-asks on the same
/// clock takes over within about one and a half lease periods of a leader dying, without a
/// database round trip on every tick of a loop that may run every second.
/// </para>
///
/// Not thread-safe, and does not need to be: each instance belongs to one background loop, which
/// asks from one place.
/// </summary>
public sealed class LeaderGate
{
    private readonly string _role;
    private readonly TimeSpan _leaseDuration;
    private readonly ILeaderLease _lease;
    private readonly ILogger _logger;

    private bool _isLeader;
    private bool _hasReported;
    private DateTime _nextCheckAt = DateTime.MinValue;

    public LeaderGate(string role, TimeSpan leaseDuration, ILeaderLease lease, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("A lease role cannot be empty.", nameof(role));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "A lease must last a positive amount of time.");

        _role = role;
        _leaseDuration = leaseDuration;
        _lease = lease;
        _logger = logger;
    }

    /// <summary>
    /// Whether this instance may run the loop for this tick.
    ///
    /// Never throws. The order-book sweep's error handling sits outside its <c>while</c> loop, so
    /// an exception escaping here would not skip one tick — it would end background matching for
    /// the lifetime of the process.
    /// </summary>
    public async Task<bool> TryLeadAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Inside the half of the lease we already claimed, the answer cannot have changed.
        if (now < _nextCheckAt) return _isLeader;

        LeaderLeaseResult result;
        try
        {
            result = await _lease.TryAcquireOrRenewAsync(_role, _leaseDuration, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // shutting down — the caller's loop is ending anyway
        }
        catch (Exception ex)
        {
            // The lease could not be confirmed, so this instance stands down until the next check.
            // Standing down on an unconfirmed lease is the safe direction: acting on a lease we
            // cannot verify is exactly the duplicate work the lease exists to prevent.
            _logger.LogWarning(ex,
                "Could not confirm the {Role} lease; this instance will not run that loop until the next check.", _role);

            Report(isLeader: false, holder: null, unconfirmed: true);
            _nextCheckAt = now.Add(_leaseDuration / 2);
            return false;
        }

        Report(result.IsLeader, result.Holder, unconfirmed: false);

        // Measured from before the call rather than after it, so a slow round trip shortens the
        // gap to the next renewal instead of eating into the lease.
        _nextCheckAt = now.Add(_leaseDuration / 2);
        return result.IsLeader;
    }

    /// <summary>
    /// Hands the role back on graceful shutdown so another instance can pick it up immediately.
    /// Does nothing if this instance was not the leader.
    /// </summary>
    public async Task ReleaseAsync(CancellationToken ct = default)
    {
        if (!_isLeader) return;

        await _lease.ReleaseAsync(_role, ct);
        _isLeader = false;
        _nextCheckAt = DateTime.MinValue;
    }

    /// <summary>
    /// Logs only when the answer changes, plus once on the first decision. A follower re-checks
    /// every half lease forever; saying so every time would bury the transition that matters.
    ///
    /// The follower line is deliberately a warning naming the other instance. Two copies of
    /// Orders.Api against one database is a legitimate thing to do now, but it is almost never
    /// intended on this deployment, and an operator scaling out for an unrelated reason needs to
    /// meet that fact somewhere other than a code comment.
    /// </summary>
    private void Report(bool isLeader, string? holder, bool unconfirmed)
    {
        var changed = !_hasReported || isLeader != _isLeader;
        _isLeader = isLeader;
        _hasReported = true;

        if (!changed || unconfirmed) return;

        if (isLeader)
        {
            _logger.LogInformation("This instance is now running the {Role} loop.", _role);
        }
        else if (holder is null)
        {
            _logger.LogWarning(
                "This instance is not running the {Role} loop: the lease is held elsewhere.", _role);
        }
        else
        {
            _logger.LogWarning(
                "This instance is NOT running the {Role} loop — instance {Holder} holds that lease. " +
                "More than one Orders.Api is running against this database; each background loop runs on one of them.",
                _role, holder);
        }
    }
}
