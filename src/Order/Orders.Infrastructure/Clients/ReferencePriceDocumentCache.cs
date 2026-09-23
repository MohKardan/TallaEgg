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
/// concurrently (issue #317) they all find the cache empty at the same instant and all fetch, which
/// is the waste this class was added to prevent. So a miss now also <b>holds the source</b>: the
/// first caller fetches while the others wait for its result rather than starting requests of their
/// own. That is the only way in, which is what makes the guarantee hold.
/// </para>
///
/// <para>
/// A failed fetch is not stored. Caching a failure would turn one transient error into a whole
/// tick's worth, and the retry costs one request on a source that is already answering badly.
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
    private readonly Dictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public ReferencePriceDocumentCache(TimeSpan duration) => _duration = duration;

    /// <summary>
    /// The source's current document: the cached one, the one another caller is already fetching,
    /// or a fresh fetch — in that order. Null when the fetch could not produce one.
    /// </summary>
    /// <param name="source">The source's name, which is what the one document belongs to.</param>
    /// <param name="fetch">
    /// How to obtain the document. Called at most once per source at a time, and never at all when
    /// a live copy is already cached.
    /// </param>
    public async Task<string?> GetOrFetchAsync(
        string source,
        Func<CancellationToken, Task<string?>> fetch,
        CancellationToken cancellationToken = default)
    {
        var cached = Cached(source);
        if (cached is not null) return cached;

        var gate = GateFor(source);
        await gate.WaitAsync(cancellationToken);

        try
        {
            // Whoever held the gate has just finished, so the document this caller wanted is
            // probably already here. Without this second look the waiting callers would queue up
            // and fetch one after another, which is the same number of requests spread over more
            // time rather than fewer requests.
            cached = Cached(source);
            if (cached is not null) return cached;

            var body = await fetch(cancellationToken);
            if (body is not null) Store(source, body);

            return body;
        }
        finally
        {
            gate.Release();
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
    /// One gate per source, so a slow source holds up only the symbols that need it. Created under
    /// the lock because two symbols reaching a source for the first time at the same instant would
    /// otherwise each create their own gate and neither would wait for the other — the exact race
    /// this class exists to remove.
    /// </summary>
    private SemaphoreSlim GateFor(string source)
    {
        lock (_lock)
        {
            if (!_gates.TryGetValue(source, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _gates[source] = gate;
            }

            return gate;
        }
    }
}
