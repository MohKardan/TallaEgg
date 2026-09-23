namespace Orders.Infrastructure.Clients;

/// <summary>
/// One fetch per price source per tick, however many symbols ask for it and whether they ask one
/// after another or all at once.
///
/// <para>
/// It exists because tgju.org and bonbast.com each answer with <b>every</b> instrument in one
/// document, while <c>AutoQuotePublisherService</c> asks per symbol and gives each ask its own DI
/// scope — so a three-symbol tick fetched the same ~180 KB document three times, and bonbast's
/// two-request page-and-token handshake three times over. Neither host publishes these endpoints
/// as an API; being cheap to serve is part of not losing them (issue #305).
/// </para>
///
/// <para>
/// Holding the last body was enough while a tick worked through its symbols in sequence: the first
/// symbol fetched and stored, and the rest read what it stored. Once the symbols of a tick run
/// concurrently (issue #317) they all find the cache empty at the same instant, so a miss now joins
/// <b>the fetch already in flight</b> instead of starting another. Sharing the in-flight fetch
/// rather than only its result is what keeps a tick as long as its slowest symbol: waiting for a
/// fetch and then repeating it would put the symbols of a failing source back in single file, which
/// is the shape this change exists to remove.
/// </para>
///
/// <para>
/// A failure is shared with whoever was waiting for it, and then forgotten. Nothing is stored, so
/// the next tick tries again; and no caller waits out a second copy of a failure that has already
/// happened. Caching failure would turn one transient error into a whole tick's worth.
/// </para>
///
/// <para>
/// Deliberately not a static field inside each provider: shared process-wide state would carry
/// one test's stubbed response into the next, and a cache nobody can reset is a cache no test can
/// tell the truth about. The lifetime is chosen once, at the composition root.
/// </para>
/// </summary>
public class ReferencePriceDocumentCache
{
    private readonly TimeSpan _duration;
    private readonly object _lock = new();
    private readonly Dictionary<string, (string Body, DateTimeOffset FetchedAt)> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<string?>> _inFlight = new(StringComparer.Ordinal);

    public ReferencePriceDocumentCache(TimeSpan duration) => _duration = duration;

    /// <summary>
    /// The source's current document: the cached one, the one another caller is already fetching,
    /// or a fresh fetch — in that order. Null when the fetch could not produce one.
    /// </summary>
    /// <param name="source">The source's name, which is what the one document belongs to.</param>
    /// <param name="fetch">
    /// How to obtain the document. Started at most once per source at a time, and not at all while
    /// a live copy is cached.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels <i>this caller's</i> wait. A fetch already in flight belongs to whoever started it
    /// and keeps running for the callers still waiting on it.
    /// </param>
    public async Task<string?> GetOrFetchAsync(
        string source,
        Func<CancellationToken, Task<string?>> fetch,
        CancellationToken cancellationToken = default)
    {
        var cached = Cached(source);
        if (cached is not null) return cached;

        var (inFlight, isOwner) = InFlightFor(source, fetch, cancellationToken);

        try
        {
            // WaitAsync, so one caller giving up does not cancel the fetch the others are waiting
            // for. The fetch itself runs on the token of whoever started it.
            var body = await inFlight.WaitAsync(cancellationToken);
            if (isOwner && body is not null) Store(source, body);

            return body;
        }
        finally
        {
            // Only the caller that started it clears it: forgetting a fetch someone else owns
            // would let a third caller start a second one alongside it.
            if (isOwner) ClearInFlight(source);
        }
    }

    private string? Cached(string source)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(source, out var entry)) return null;

            if (DateTimeOffset.UtcNow - entry.FetchedAt >= _duration)
            {
                _entries.Remove(source);
                return null;
            }

            return entry.Body;
        }
    }

    private void Store(string source, string body)
    {
        lock (_lock)
        {
            _entries[source] = (body, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// The fetch for this source: the one already running, or a new one this caller owns. Started
    /// under the lock because two symbols reaching a source at the same instant would otherwise
    /// each start their own and neither would see the other — the exact race this class removes.
    /// </summary>
    private (Task<string?> Task, bool IsOwner) InFlightFor(
        string source, Func<CancellationToken, Task<string?>> fetch, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_inFlight.TryGetValue(source, out var existing)) return (existing, false);

            // Started inside the lock but not awaited here: the lock is held only long enough to
            // record that this source is being fetched.
            var started = fetch(cancellationToken);
            _inFlight[source] = started;

            return (started, true);
        }
    }

    private void ClearInFlight(string source)
    {
        lock (_lock)
        {
            _inFlight.Remove(source);
        }
    }
}
