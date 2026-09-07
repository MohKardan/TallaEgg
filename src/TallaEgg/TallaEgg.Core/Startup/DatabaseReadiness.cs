namespace TallaEgg.Core.Startup;

/// <summary>
/// Whether this service's database migration has finished (issue #230).
/// </summary>
/// <remarks>
/// One instance per service, registered as a singleton. It is written exactly once, by
/// <see cref="DatabaseMigrationHostedService"/>, and read on every request by the readiness gate
/// — so the flag is <c>volatile</c> rather than locked: the writer never clears it, and a reader
/// that observes the old value merely answers one more 503 before it sees the new one.
/// </remarks>
public sealed class DatabaseReadiness
{
    private volatile bool _isReady;

    /// <summary>
    /// True once the migration has succeeded, false from process start until then.
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// Records that the migration succeeded, which opens the readiness gate.
    /// </summary>
    public void MarkReady() => _isReady = true;
}
