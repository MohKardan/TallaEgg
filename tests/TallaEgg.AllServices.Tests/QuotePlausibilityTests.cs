using Orders.Core;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The band itself (issue #158), now that both ways of setting a price share it.
///
/// <para>
/// It began inside the auto-publisher, on the theory that the price feed was the thing that could
/// produce a wrong number. An admin typing one zero too many does exactly the same damage, and a
/// manual quote is the more dangerous of the two — it is tradeable the instant it lands, and nobody
/// is watching a log for it. Testing the rule on its own is what keeps the two paths honest.
/// </para>
/// </summary>
public class QuotePlausibilityTests
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;

    private static Quote QuoteAt(decimal buy, decimal sell) =>
        Quote.Publish(Symbol, buy, sell, Guid.NewGuid());

    /// <summary>
    /// A symbol with no quote has nothing for a price to be implausible relative to. That is one
    /// unguarded price per symbol, ever, and only while no quote exists at all.
    /// </summary>
    [Fact]
    public void WithNoCurrentQuoteAnythingIsWithinBand()
    {
        var verdict = QuotePlausibility.Check(999_999_999m, null);

        Assert.True(verdict.IsWithinBand);
        Assert.Null(verdict.PreviousMid);
        Assert.Equal(0m, verdict.DeviationPercent);
    }

    [Theory]
    [InlineData(1_000_000, true)]    // unchanged
    [InlineData(1_040_000, true)]    // +4%: remarkable in two minutes, but a price
    [InlineData(1_050_000, true)]    // exactly +5%: the edge is inclusive
    [InlineData(1_050_001, false)]   // a hair past it
    [InlineData(960_000, true)]      // -4%
    [InlineData(949_999, false)]     // past it downwards: the band is two-sided
    public void APriceIsJudgedAgainstTheCurrentMid(decimal proposedMid, bool expectedWithinBand)
    {
        var verdict = QuotePlausibility.Check(proposedMid, QuoteAt(995_000m, 1_005_000m));

        Assert.Equal(expectedWithinBand, verdict.IsWithinBand);
        Assert.Equal(1_000_000m, verdict.PreviousMid);
    }

    /// <summary>
    /// The unit slip the issue names: gold sources quote per mithqal natively, and a mithqal is
    /// about 4.33 grams, so a conversion that fails to happen multiplies the price by that much.
    /// </summary>
    [Fact]
    public void AMithqalReadAsAGramIsFarOutsideTheBand()
    {
        var verdict = QuotePlausibility.Check(34_640_000m, QuoteAt(7_960_000m, 8_040_000m));

        Assert.False(verdict.IsWithinBand);
        Assert.True(verdict.DeviationPercent > 300m, $"expected a huge deviation, got {verdict.DeviationPercent}");
    }

    /// <summary>The mistyped zero, which is what brought the band to the manual path.</summary>
    [Theory]
    [InlineData(100_000)]      // one zero short
    [InlineData(10_000_000)]   // one zero too many
    public void AMisplacedZeroIsOutsideTheBand(decimal proposedMid)
    {
        Assert.False(QuotePlausibility.Check(proposedMid, QuoteAt(995_000m, 1_005_000m)).IsWithinBand);
    }

    /// <summary>
    /// Comparing mids rather than either leg keeps a spread change from reading as a price move: the
    /// shop widening its spread has not changed what it thinks the asset is worth.
    /// </summary>
    [Fact]
    public void WideningTheSpreadAroundTheSameMidStaysWithinBand()
    {
        var verdict = QuotePlausibility.Check(
            QuotePlausibility.MidOf(900_000m, 1_100_000m), QuoteAt(995_000m, 1_005_000m));

        Assert.True(verdict.IsWithinBand);
        Assert.Equal(0m, verdict.DeviationPercent);
    }

    /// <summary>
    /// The band is a ratio, so it should not care how large the numbers are — but Bitcoin prices are
    /// five orders of magnitude above gold's and the simulator only ever trades MAUA/IRT (#147), so
    /// a scale-specific problem would survive a clean smoke run.
    /// </summary>
    [Fact]
    public void TheBandBehavesTheSameAtBitcoinScale()
    {
        var current = QuoteAt(51_740_000_000m, 52_260_000_000m);   // mid 52,000,000,000

        Assert.True(QuotePlausibility.Check(54_600_000_000m, current).IsWithinBand);    // exactly +5%
        Assert.False(QuotePlausibility.Check(5_460_000_000m, current).IsWithinBand);    // a decimal slipped
    }
}
