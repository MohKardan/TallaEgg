namespace Orders.Core;

/// <summary>
/// A price from an external source, with the moment that source says it was true.
///
/// <para>
/// The second half is the reason this type exists (issue #316). Providers used to return a bare
/// <c>decimal?</c>, so a market that had closed, a feed that had frozen and a live price were
/// indistinguishable, and the last value before a market shut was published as the current one.
/// <c>QuotePlausibility</c> cannot catch that: it measures how far a price has moved, and the
/// problem is a price that has stopped moving.
/// </para>
/// </summary>
/// <param name="Price">
/// The price in the symbol's quote currency, per one unit of its base asset. Each provider
/// converts its source's own units before constructing this.
/// </param>
/// <param name="AsOf">
/// When the source says the price was last true, or null when it does not say. Null means
/// "unknown", never "now": an age check treats it as unmeasurable and lets the price through, so
/// that adding this field could not silently stop any symbol quoting.
/// </param>
public readonly record struct ReferencePrice(decimal Price, DateTimeOffset? AsOf);
