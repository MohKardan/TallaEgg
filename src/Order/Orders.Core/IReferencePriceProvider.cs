namespace Orders.Core;

/// <summary>
/// A source of the current external reference price for a trading pair, in Toman per one whole
/// unit of the base asset — a gram of melted gold, a full Bahar Azadi coin, one Bitcoin. Multiple
/// implementations exist so <c>ReferencePriceProviderChain</c> can fall back from one external
/// service to another; adding a third or fourth source later is just one more class implementing
/// this interface.
///
/// <para>
/// Originally gold-only (<c>IGoldPriceProvider.GetMesghalPriceAsync</c>), generalized to any
/// configured symbol when coin and Bitcoin quoting were added. "Per mesghal" was never a property
/// of the interface — it was nerkh.io's and brsapi.ir's native gold unit, converted to per-gram
/// before use. That conversion, and any other unit conversion a provider's upstream API needs,
/// now happens inside each provider, per symbol, so callers only ever see Toman per traded unit.
/// </para>
/// </summary>
public interface IReferencePriceProvider
{
    /// <summary>A short name for logging which provider answered or was skipped.</summary>
    string Name { get; }

    /// <summary>
    /// The current price in Toman per one unit of <paramref name="symbol"/>'s base asset, or null
    /// if this provider has no data for that symbol or could not answer (network failure,
    /// invalid/missing credentials, unexpected response shape). Never throws — a provider that
    /// cannot answer is reported by returning null, not by an exception, so the chain can move to
    /// the next one without a try/catch at every call site.
    /// </summary>
    Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default);
}
