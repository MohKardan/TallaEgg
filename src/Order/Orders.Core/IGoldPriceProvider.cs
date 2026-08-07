namespace Orders.Core;

/// <summary>
/// A source of the current melted 18k gold ("آبشده") price, in Toman per mesghal — the same
/// unit and product an admin already prices manually. Multiple implementations exist so
/// <c>GoldPriceProviderChain</c> can fall back from one external service to another; adding a
/// third or fourth source later is just one more class implementing this interface.
/// </summary>
public interface IGoldPriceProvider
{
    /// <summary>A short name for logging which provider answered or was skipped.</summary>
    string Name { get; }

    /// <summary>
    /// The current price in Toman per mesghal, or null if this provider could not answer
    /// (network failure, invalid/missing token, unexpected response shape). Never throws —
    /// a provider that cannot answer is reported by returning null, not by an exception, so the
    /// chain can move to the next one without a try/catch at every call site.
    /// </summary>
    Task<decimal?> GetMesghalPriceAsync(CancellationToken cancellationToken = default);
}
