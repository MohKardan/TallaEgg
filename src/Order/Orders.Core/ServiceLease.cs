namespace Orders.Core;

/// <summary>
/// A named, time-limited claim on work only one instance may perform at a time (issue #160).
///
/// The outbox can be coordinated per row, because each unit of work is a row an instance can put
/// its name on. The background loops cannot: <c>MatchingEngineService</c> sweeps the whole order
/// book on a timer and <c>AutoQuotePublisherService</c> publishes on a timer, and neither has a
/// queue row to claim. What they share is that only one instance should be running the loop, so
/// what gets claimed is the role itself — one row per role, held for a while, renewed while the
/// holder is alive.
///
/// The expiry does the same job as it does on an outbox message: an instance that dies holding
/// the role does not take the role down with it. The gap between the holder dying and the lease
/// expiring is a gap in which nobody sweeps or publishes, which is why the holder also releases
/// the lease on a graceful shutdown instead of leaving it to time out.
/// </summary>
public class ServiceLease
{
    /// <summary>Names the work being claimed — see <see cref="ServiceLeaseRoles"/>. The key: one row per role.</summary>
    public string Role { get; private set; } = "";

    /// <summary>The <c>InstanceIdentity</c> of the holder.</summary>
    public string Owner { get; private set; } = "";

    /// <summary>When the current holder first took the role, kept for diagnosing flapping.</summary>
    public DateTime AcquiredAt { get; private set; }

    /// <summary>When the claim stops being honoured and another instance may take the role.</summary>
    public DateTime ExpiresAt { get; private set; }

    // EF Core
    private ServiceLease() { }

    /// <summary>
    /// Creates the row for a role nobody has ever held. Only used on the insert path — from then
    /// on the row exists and is taken over by UPDATE, never re-created.
    /// </summary>
    public static ServiceLease CreateHeldBy(string role, string owner, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("A lease role cannot be empty.", nameof(role));
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("A lease owner cannot be empty.", nameof(owner));

        return new ServiceLease
        {
            Role = role,
            Owner = owner,
            AcquiredAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
    }
}

/// <summary>
/// The roles that are coordinated. Constants rather than free strings so a typo in one service
/// cannot silently give it a role of its own that nothing else contends for — which would look
/// exactly like working correctly right up until two instances ran.
/// </summary>
public static class ServiceLeaseRoles
{
    /// <summary>The background order-book sweep. Does not cover request-path matching.</summary>
    public const string MatchingEngine = "MatchingEngine";

    /// <summary>The automatic quote publishing tick.</summary>
    public const string AutoQuotePublisher = "AutoQuotePublisher";
}
