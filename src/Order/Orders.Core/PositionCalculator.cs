namespace Orders.Core;

/// <summary>
/// One trade from a single participant's point of view, stripped down to what FIFO
/// matching needs. A buy is a positive <see cref="SignedQuantity"/>, a sell negative — the
/// caller (which knows whether this participant was the trade's buyer or seller) does that
/// translation; this type has no notion of "buyer"/"seller" itself.
/// </summary>
public readonly record struct PositionTradeLeg(DateTime OccurredAt, decimal SignedQuantity, decimal Price, decimal Fee);

/// <summary>
/// The result of matching one participant's trades in one symbol.
/// <see cref="RemainingQuantity"/> is signed: positive is a long (net bought) position,
/// negative a short (net sold, credit-backed) position, zero is flat.
/// <see cref="AverageCost"/> is null when flat — there is no cost basis for nothing held.
/// </summary>
public readonly record struct PositionResult(
    decimal RealizedPnl,
    decimal RemainingQuantity,
    decimal? AverageCost,
    decimal TotalFees);

/// <summary>
/// Matches a participant's trades in one symbol FIFO — oldest lot closed first — to produce
/// realized P&amp;L and the cost basis of whatever remains open (issue #93).
///
/// <para>
/// <b>Why FIFO, not weighted average:</b> a deliberate choice (not a default), because it
/// gives a more familiar audit trail — "which specific purchase did this sale close" — even
/// though it costs more to compute than a running average.
/// </para>
///
/// <para>
/// <b>Long and short use the same formula.</b> An open lot is just a signed quantity at a
/// price; closing one — a sell against a long lot, or a buy against a short lot — realizes
/// <c>closedQty * (exitPrice - lotPrice) * sign(lotQty)</c>. For a long lot that is the
/// familiar "sold higher than bought = profit"; for a short lot the sign flip makes "bought
/// back lower than sold = profit" fall out of the identical line, so a credit-backed short
/// position (issue #61, #93's acceptance criteria) needs no separate branch.
/// </para>
///
/// <para>
/// <b>Fees are expensed at trade time, not capitalized into the lot.</b> Every trade's fee
/// (read from the <c>Trade</c> row, never assumed zero — see issue #35) reduces realized
/// P&amp;L in the period it was paid, whether that trade opened or closed a lot. This is the
/// simpler of two defensible treatments — the alternative, folding a fee into the lot's cost
/// basis so it is only recognized when that lot eventually closes, is arguably more correct
/// but adds real complexity for a scenario that cannot happen yet (fees are hardcoded to
/// zero today; #35 is what would turn them on). Revisit this once fees are real.
/// </para>
/// </summary>
public static class PositionCalculator
{
    public static PositionResult Calculate(IEnumerable<PositionTradeLeg> legs)
    {
        var ordered = legs.OrderBy(l => l.OccurredAt).ToList();

        // FIFO queue of open lots, oldest first. A lot's sign marks long (bought, awaiting a
        // sell) vs short (sold, awaiting a buy-back).
        var lots = new List<(decimal Quantity, decimal Price)>();

        var realizedPnl = 0m;
        var totalFees = 0m;

        foreach (var leg in ordered)
        {
            totalFees += leg.Fee;
            realizedPnl -= leg.Fee;

            var remaining = leg.SignedQuantity;

            while (remaining != 0 && lots.Count > 0 && Math.Sign(lots[0].Quantity) != Math.Sign(remaining))
            {
                var (lotQuantity, lotPrice) = lots[0];
                var closedQuantity = Math.Min(Math.Abs(remaining), Math.Abs(lotQuantity));

                realizedPnl += closedQuantity * (leg.Price - lotPrice) * Math.Sign(lotQuantity);

                var remainingLotQuantity = lotQuantity - Math.Sign(lotQuantity) * closedQuantity;
                if (remainingLotQuantity == 0)
                    lots.RemoveAt(0);
                else
                    lots[0] = (remainingLotQuantity, lotPrice);

                remaining -= Math.Sign(remaining) * closedQuantity;
            }

            if (remaining != 0)
                lots.Add((remaining, leg.Price));
        }

        var remainingQuantity = lots.Sum(l => l.Quantity);
        decimal? averageCost = remainingQuantity == 0
            ? null
            : lots.Sum(l => l.Quantity * l.Price) / remainingQuantity;

        return new PositionResult(realizedPnl, remainingQuantity, averageCost, totalFees);
    }
}
