using Orders.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The FIFO position/P&amp;L engine (issue #93), tested independently of the DB and the
/// service that will feed it real trades — this is pure arithmetic and should be pinned
/// down as such.
/// </summary>
public class PositionCalculatorTests
{
    private static PositionTradeLeg Buy(decimal qty, decimal price, decimal fee = 0m, int daysAgo = 0) =>
        new(DateTime.UtcNow.AddDays(-daysAgo), qty, price, fee);

    private static PositionTradeLeg Sell(decimal qty, decimal price, decimal fee = 0m, int daysAgo = 0) =>
        new(DateTime.UtcNow.AddDays(-daysAgo), -qty, price, fee);

    [Fact]
    public void NoTrades_IsFlatWithNoRealizedPnlAndNoCostBasis()
    {
        var result = PositionCalculator.Calculate([]);

        Assert.Equal(0m, result.RealizedPnl);
        Assert.Equal(0m, result.RemainingQuantity);
        Assert.Null(result.AverageCost);
        Assert.Equal(0m, result.TotalFees);
    }

    [Fact]
    public void ASingleBuyThenAMatchingSell_RealizesTheFullGain()
    {
        var result = PositionCalculator.Calculate([
            Buy(10m, 100m, daysAgo: 2),
            Sell(10m, 150m, daysAgo: 1)
        ]);

        Assert.Equal(500m, result.RealizedPnl); // 10 * (150 - 100)
        Assert.Equal(0m, result.RemainingQuantity);
        Assert.Null(result.AverageCost);
    }

    [Fact]
    public void ASingleBuyThenAMatchingSellAtALoss_RealizesTheLossAsNegative()
    {
        var result = PositionCalculator.Calculate([
            Buy(10m, 150m, daysAgo: 2),
            Sell(10m, 100m, daysAgo: 1)
        ]);

        Assert.Equal(-500m, result.RealizedPnl);
    }

    /// <summary>
    /// The case FIFO and weighted-average actually disagree on: the sell closes the OLDER
    /// lot first, so the remainder held is priced at the newer lot's cost, not a blended
    /// average of both.
    /// </summary>
    [Fact]
    public void SeveralBuysAtDifferentPricesThenAPartialSell_ClosesTheOldestLotFirst()
    {
        var result = PositionCalculator.Calculate([
            Buy(5m, 100m, daysAgo: 3),
            Buy(3m, 110m, daysAgo: 2),
            Sell(6m, 150m, daysAgo: 1)
        ]);

        // Closes all 5 of the first lot (gain 5*50=250) plus 1 of the second (gain 1*40=40).
        Assert.Equal(290m, result.RealizedPnl);

        // 2 units left over from the second (110) lot -- not a blended average of 100 and 110.
        Assert.Equal(2m, result.RemainingQuantity);
        Assert.Equal(110m, result.AverageCost);
    }

    /// <summary>
    /// A remaining position spanning multiple still-open lots of the same sign is averaged
    /// across them -- this is the one place a weighted mean legitimately appears, for the
    /// leftover, not as the costing method itself.
    /// </summary>
    [Fact]
    public void MultipleOpenLotsRemaining_AreAveragedTogether()
    {
        var result = PositionCalculator.Calculate([
            Buy(3m, 100m, daysAgo: 2),
            Buy(5m, 120m, daysAgo: 1)
        ]);

        Assert.Equal(0m, result.RealizedPnl); // nothing closed yet
        Assert.Equal(8m, result.RemainingQuantity);
        Assert.Equal((3m * 100m + 5m * 120m) / 8m, result.AverageCost);
    }

    /// <summary>
    /// A credit-backed short: selling before ever holding the asset. The same formula that
    /// handles a long position must produce the correct sign here without a special case.
    /// </summary>
    [Fact]
    public void ASellWithNoPriorPosition_OpensAShort()
    {
        var result = PositionCalculator.Calculate([
            Sell(4m, 100m, daysAgo: 1)
        ]);

        Assert.Equal(-4m, result.RemainingQuantity); // negative: short
        Assert.Equal(100m, result.AverageCost);
        Assert.Equal(0m, result.RealizedPnl); // nothing closed yet
    }

    [Fact]
    public void CoveringAShortAtALowerPrice_RealizesAGain()
    {
        var result = PositionCalculator.Calculate([
            Sell(4m, 100m, daysAgo: 2),  // short 4 @ 100
            Buy(4m, 60m, daysAgo: 1)     // covers at a lower price -- a gain for the short
        ]);

        Assert.Equal(160m, result.RealizedPnl); // 4 * (100 - 60)
        Assert.Equal(0m, result.RemainingQuantity);
    }

    [Fact]
    public void CoveringAShortAtAHigherPrice_RealizesALoss()
    {
        var result = PositionCalculator.Calculate([
            Sell(4m, 100m, daysAgo: 2),
            Buy(4m, 130m, daysAgo: 1)
        ]);

        Assert.Equal(-120m, result.RealizedPnl); // 4 * (100 - 130)
    }

    /// <summary>
    /// A buy larger than the open short first covers it (realizing P&amp;L), then the
    /// remainder opens a fresh long lot -- crossing through flat, not two disconnected
    /// positions.
    /// </summary>
    [Fact]
    public void ABuyThatOvershootsAnOpenShort_ClosesItThenOpensALong()
    {
        var result = PositionCalculator.Calculate([
            Sell(4m, 100m, daysAgo: 2),   // short 4 @ 100
            Buy(6m, 80m, daysAgo: 1)      // covers 4 (gain) and opens a new long of 2 @ 80
        ]);

        Assert.Equal(80m, result.RealizedPnl); // 4 * (100 - 80)
        Assert.Equal(2m, result.RemainingQuantity);
        Assert.Equal(80m, result.AverageCost);
    }

    /// <summary>Fees are read per trade and expensed immediately, never assumed to be zero (#35).</summary>
    [Fact]
    public void FeesReduceRealizedPnlAndAreTotalledSeparately()
    {
        var result = PositionCalculator.Calculate([
            Buy(10m, 100m, fee: 5m, daysAgo: 2),
            Sell(10m, 150m, fee: 7m, daysAgo: 1)
        ]);

        Assert.Equal(500m - 12m, result.RealizedPnl); // gain minus both fees
        Assert.Equal(12m, result.TotalFees);
    }

    [Fact]
    public void TradesAreMatchedInChronologicalOrder_RegardlessOfInputOrder()
    {
        // Same trades as the "closes the oldest lot first" case, but handed to the
        // calculator out of order -- OccurredAt must be what determines FIFO order, not
        // the order the caller happened to enumerate them in.
        var result = PositionCalculator.Calculate([
            Sell(6m, 150m, daysAgo: 1),
            Buy(3m, 110m, daysAgo: 2),
            Buy(5m, 100m, daysAgo: 3)
        ]);

        Assert.Equal(290m, result.RealizedPnl);
        Assert.Equal(2m, result.RemainingQuantity);
        Assert.Equal(110m, result.AverageCost);
    }
}
