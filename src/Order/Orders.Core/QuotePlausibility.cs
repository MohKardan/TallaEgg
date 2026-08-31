namespace Orders.Core;

/// <summary>
/// How far a proposed quote may sit from the one currently published before a human has to look
/// at it (issue #158).
///
/// <para>
/// This lives in the domain rather than inside the auto-publisher because both ways of setting a
/// price need the same rule. It began as a guard on the price feed, on the theory that the
/// machine was the thing that could produce a wrong number; the admin typing one zero too many
/// does exactly the same damage, and a manually published quote is worse, because it is
/// immediately tradeable and nobody is watching a log for it.
/// </para>
/// </summary>
public static class QuotePlausibility
{
    /// <summary>
    /// How far a proposed price may sit from the last published quote's mid, as a percentage.
    ///
    /// <para>
    /// A business number rather than an engineering one, set by the product owner: gold does not
    /// move 5% in the two minutes between auto-quote ticks, so a source that says it did is
    /// reporting a broken feed rather than a market. Deliberately far wider than any real move and
    /// still far narrower than the mistakes it catches — a per-mithqal figure read as per-gram is
    /// roughly 4.33x, and a misplaced decimal, by hand or by machine, is 10x.
    /// </para>
    /// </summary>
    public const decimal MaxDeviationPercent = 5m;

    /// <summary>
    /// The verdict on one proposed quote.
    /// </summary>
    /// <param name="IsWithinBand">Whether it may be published without asking anyone.</param>
    /// <param name="DeviationPercent">How far it sits from <paramref name="PreviousMid"/>; zero when there is nothing to compare against.</param>
    /// <param name="PreviousMid">The mid it was measured against, or null on a symbol that has never had a quote.</param>
    public readonly record struct Verdict(bool IsWithinBand, decimal DeviationPercent, decimal? PreviousMid);

    /// <summary>
    /// Measures a proposed quote against the symbol's current one.
    ///
    /// <para>
    /// The comparison is mid to mid. For an auto-published quote the mid is exactly the reference
    /// price it was built from, since the spread is applied symmetrically either side; for a
    /// manual one it is the midpoint of what the admin typed. Comparing mids rather than the buy
    /// or sell leg keeps a spread change from reading as a price move.
    /// </para>
    ///
    /// <para>
    /// A symbol with no active quote has nothing for a price to be implausible relative to, so the
    /// first quote is always within band. That is one unguarded price per symbol, ever — and only
    /// while no quote exists at all.
    /// </para>
    /// </summary>
    public static Verdict Check(decimal proposedMid, Quote? currentQuote)
    {
        if (currentQuote is null)
            return new Verdict(IsWithinBand: true, DeviationPercent: 0m, PreviousMid: null);

        var previousMid = MidOf(currentQuote);

        // Quote.Publish requires both prices to be positive, so the mid cannot be zero here.
        var deviationPercent = Math.Abs(proposedMid - previousMid) / previousMid * 100m;

        return new Verdict(deviationPercent <= MaxDeviationPercent, deviationPercent, previousMid);
    }

    /// <summary>The midpoint of a quote's two legs.</summary>
    public static decimal MidOf(Quote quote) => (quote.BuyPrice + quote.SellPrice) / 2m;

    /// <summary>The midpoint of a proposed pair of prices, before any <see cref="Quote"/> exists.</summary>
    public static decimal MidOf(decimal buyPrice, decimal sellPrice) => (buyPrice + sellPrice) / 2m;
}
