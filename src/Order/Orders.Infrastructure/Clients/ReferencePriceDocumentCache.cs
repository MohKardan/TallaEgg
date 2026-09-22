namespace Orders.Infrastructure.Clients;

/// <summary>
/// Holds the last response body fetched from each price source for a short while, so that one
/// publisher tick pulls each source once rather than once per symbol.
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

    public ReferencePriceDocumentCache(TimeSpan duration) => _duration = duration;

    /// <summary>The cached body for a source, or null when nothing was stored or it has expired.</summary>
    public string? Get(string source)
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

    public void Set(string source, string body)
    {
        lock (_lock)
        {
            _entries[source] = (body, DateTimeOffset.UtcNow);
        }
    }
}
