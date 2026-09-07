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
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private volatile bool _isReady;

    /// <summary>
    /// True once the migration has succeeded, false from process start until then.
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// Records that the migration succeeded, which opens the readiness gate.
    /// </summary>
    public void MarkReady()
    {
        // The flag first, so a loop woken by the task below never observes a stale false.
        _isReady = true;
        _ready.TrySetResult();
    }

    /// <summary>
    /// Waits for the migration to succeed. Returns <c>false</c> instead of throwing when the host
    /// shuts down first, so a caller in a <c>BackgroundService</c> can simply stop.
    /// </summary>
    /// <remarks>
    /// For in-process work that the readiness gate cannot cover: the gate turns away HTTP
    /// requests, but a background loop reaches the database without one. Since the migration runs
    /// alongside the host rather than ahead of it (issue #230), a loop that queried straight away
    /// could read a schema mid-migration — and an exception escaping a <c>BackgroundService</c>
    /// stops the whole host under the default
    /// <c>BackgroundServiceExceptionBehavior.StopHost</c>, which is the outage this issue exists
    /// to remove.
    /// </remarks>
    public async Task<bool> WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _ready.Task.WaitAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
