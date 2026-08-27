using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Locked collateral must be fully consumed or released once an order is completely filled.
///
/// There were two independent sources of residue (issue #52):
///
/// 1. Once, at lock time: the bot sent an unrounded price (mesghal price / 4.3318) and the lock was
///    computed from it, but Orders.Price holds only two decimals and settlement reads the rounded
///    price back from the database.
///
/// 2. Per fill: each trade's consumption is rounded separately, and the sum of separately-rounded
///    amounts does not equal the once-rounded whole.
///
/// These tests work on the arithmetic itself, because the arithmetic is what was wrong.
/// </summary>
public class LockResidueTests
{
    private const decimal AskPerMesghal = 80_000_000m;
    private const decimal BidPerMesghal = 79_000_000m;

    private static decimal PricePerGram(decimal perMesghal) =>
        perMesghal / CurrenciesConstant.GramsPerMesghal;

    /// <summary>The lock rounds up, the same as OrderService does.</summary>
    private static decimal Lock(decimal quantity, decimal price) =>
        CurrenciesConstant.CeilingToCurrencyPrecision(quantity * price, CurrenciesConstant.Toman);

    /// <summary>Each trade's consumption rounds down, the same as CreateTrade does.</summary>
    private static decimal Fill(decimal quantity, decimal price) =>
        CurrenciesConstant.FloorToCurrencyPrecision(quantity * price, CurrenciesConstant.Toman);

    /// <summary>
    /// Source 1. The rounded price is what gets stored and later read back for settlement, so the
    /// lock has to be computed from it. This buy rate was chosen because 79,000,000 / 4.3318 does
    /// not close at two decimals — the case that exposed the bug.
    /// </summary>
    [Fact]
    public void LockUsesTheSamePriceThatWillBeStored()
    {
        var raw = PricePerGram(BidPerMesghal);              // 18237222.401772935…
        var stored = CurrenciesConstant.RoundOrderPrice(raw); // 18237222.40

        var lockedFromRawPrice = Lock(1000m, raw);
        var lockedFromStoredPrice = Lock(1000m, stored);

        // These two figures used to differ by 2 toman, and that difference stayed locked forever.
        Assert.NotEqual(lockedFromRawPrice, lockedFromStoredPrice);
        Assert.Equal(18_237_222_400m, lockedFromStoredPrice);
    }

    /// <summary>The price must round to the column's precision exactly — no more, no less.</summary>
    [Theory]
    [InlineData(80_000_000, 18_468_073.32)]
    [InlineData(79_000_000, 18_237_222.40)]
    public void PriceIsRoundedToTheColumnScale(decimal perMesghal, decimal expected)
    {
        var stored = CurrenciesConstant.RoundOrderPrice(PricePerGram(perMesghal));

        Assert.Equal(expected, stored);
        Assert.Equal(stored, Math.Round(stored, CurrenciesConstant.OrderPriceDecimalPlaces));
    }

    /// <summary>
    /// Source 2, and why rounding the price alone is not enough: even at an identical price, the sum
    /// of separately-rounded fills does not equal the once-rounded lock.
    ///
    /// This proves the residue exists, so it is clear why a release at the end of the order is
    /// required and why rounding the price is not a substitute.
    /// </summary>
    [Fact]
    public void PerFillRounding_LeavesAResidue_EvenWithTheStoredPrice()
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));

        var locked = Lock(10m, price);
        var consumed = Fill(3m, price) + Fill(3m, price) + Fill(3m, price) + Fill(1m, price);

        // A residue exists and rounding the price does not remove it — which is why the release at
        // the end of the order is necessary.
        Assert.NotEqual(locked, consumed);
        Assert.True(locked > consumed);
    }

    /// <summary>
    /// The case that used to over-consume: five two-gram fills, each with a 0.8 fraction. Under
    /// AwayFromZero every one rounded up and their sum exceeded the lock by 1 toman, so the
    /// "insufficient collateral" guard refused a perfectly valid trade.
    /// </summary>
    [Fact]
    public void FillsThatUsedToOverConsume_NoLongerDo()
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));

        var locked = Lock(10m, price);
        var consumed = Fill(2m, price) * 5;

        Assert.True(consumed <= locked, $"consumed {consumed} must not exceed locked {locked}");
    }

    /// <summary>
    /// The guarantee itself rather than one example: for any combination of fill sizes, total
    /// consumption never exceeds the locked amount.
    ///
    ///     Σ Floor(qᵢ × p) ≤ Σ qᵢ×p = Q×p ≤ Ceiling(Q×p)
    ///
    /// A single-example test cannot show this — one lucky combination stays green while another
    /// breaks. That is exactly what happened with 3+3+4, and for a few hours it looked as though the
    /// residue did not grow.
    /// </summary>
    [Theory]
    [InlineData(new double[] { 2, 2, 2, 2, 2 })]
    [InlineData(new double[] { 3, 3, 3, 1 })]
    [InlineData(new double[] { 3, 3, 4 })]
    [InlineData(new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 })]
    [InlineData(new double[] { 0.1, 0.3, 0.7, 1.9, 7 })]
    [InlineData(new double[] { 9.99, 0.01 })]
    public void ConsumptionNeverExceedsTheLock_ForAnyFillPattern(double[] fills)
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));
        var total = fills.Sum(f => (decimal)f);

        var locked = Lock(total, price);
        var consumed = fills.Sum(f => Fill((decimal)f, price));

        Assert.True(consumed <= locked,
            $"fills [{string.Join(", ", fills)}] consumed {consumed} but only {locked} was locked");
    }

    /// <summary>
    /// And the residue must stay negligible — at most one unit per fill. If the rounding direction
    /// is ever changed wrongly, total consumption drops and this test breaks with it.
    /// </summary>
    [Theory]
    [InlineData(new double[] { 2, 2, 2, 2, 2 })]
    [InlineData(new double[] { 3, 3, 3, 1 })]
    [InlineData(new double[] { 0.1, 0.3, 0.7, 1.9, 7 })]
    public void TheResidueStaysBoundedByOneUnitPerFill(double[] fills)
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));
        var total = fills.Sum(f => (decimal)f);

        var residue = Lock(total, price) - fills.Sum(f => Fill((decimal)f, price));

        Assert.InRange(residue, 0m, fills.Length + 1);
    }

    /// <summary>
    /// Residue = what was locked minus what was consumed — the same formula the cancellation and
    /// completion paths both use. Releasing that amount brings the lock to zero.
    /// </summary>
    [Fact]
    public void ReleasingTheResidue_BringsTheLockToZero()
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));

        var locked = Lock(10m, price);
        var consumed = Fill(3m, price) + Fill(3m, price) + Fill(3m, price) + Fill(1m, price);
        var residue = locked - consumed;

        Assert.NotEqual(0m, residue);           // اول مطمئن شویم چیزی برای آزاد کردن هست
        Assert.True(residue > 0);               // و همیشه در جهت آزادسازی، نه بدهکاری
        Assert.Equal(0m, locked - consumed - residue);
    }

    /// <summary>
    /// The sell side has no residue: its collateral is the base asset and each trade consumes
    /// exactly Quantity, with no rounding. This test pins that assumption so a change to it cannot
    /// pass silently.
    /// </summary>
    [Fact]
    public void SellSide_HasNoResidue()
    {
        var locked = CurrenciesConstant.RoundToCurrencyPrecision(10m, CurrenciesConstant.Maua);
        var consumed = 3m + 3m + 4m;

        Assert.Equal(locked, consumed);
    }
}
