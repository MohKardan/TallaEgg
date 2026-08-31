namespace Orders.Application.Services;

/// <summary>
/// Decides which instance runs a background loop that must have exactly one writer (issue #160).
///
/// The contract is deliberately narrow: ask whether you may run, and say when you stop. Everything
/// about how long a lease lasts and when it is renewed belongs to the caller, because the two
/// loops using this run on very different clocks — the order-book sweep every second, quote
/// publishing every two minutes.
/// </summary>
public interface ILeaderLease
{
    /// <summary>
    /// Claims <paramref name="role"/> for this instance for <paramref name="duration"/>, or renews
    /// a claim it already holds. Returns whether this instance may act.
    ///
    /// Both are one operation on purpose. A renewal and a takeover differ only in who held the
    /// role a moment ago, and asking "am I still the leader?" separately from "may I become the
    /// leader?" opens a window between the two questions in which the answer changes.
    /// </summary>
    Task<LeaderLeaseResult> TryAcquireOrRenewAsync(string role, TimeSpan duration, CancellationToken ct = default);

    /// <summary>
    /// Gives up <paramref name="role"/> if this instance holds it, so the next instance can take
    /// over at once instead of waiting out the lease. Called on graceful shutdown; a crash skips
    /// it and the expiry does the same job more slowly.
    ///
    /// Never throws: it runs while the host is already stopping, where an exception would be
    /// noise at best and would mask the real reason for the shutdown at worst.
    /// </summary>
    Task ReleaseAsync(string role, CancellationToken ct = default);
}

/// <summary>
/// The outcome of a claim. <paramref name="Holder"/> is filled in when the claim was refused and
/// the current holder could be read — it is what turns "this instance is idle" in the log into
/// "another instance is running this loop", which is the whole point of noticing a second
/// deployment.
/// </summary>
public readonly record struct LeaderLeaseResult(bool IsLeader, string? Holder)
{
    public static LeaderLeaseResult Leader { get; } = new(true, null);

    public static LeaderLeaseResult FollowerOf(string? holder) => new(false, holder);
}
