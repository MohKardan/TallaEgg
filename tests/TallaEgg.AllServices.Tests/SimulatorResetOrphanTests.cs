using TallaEgg.TelegramBot.Simulator;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The simulator's <c>DataReset</c> must delete <b>both</b> sides of every trade a simulated
/// user was part of, not just the side the simulated user owns (issue #257).
///
/// <para>
/// A quote fill creates the customer's order and the market maker's, and the market maker is a
/// real account by design — so deleting orders by owner leaves the dealer's half behind with
/// its trade gone. On one dev machine that had accumulated 4,451 orphans out of 4,503 orders,
/// and issue #250 measured them believing they were product data.
/// </para>
///
/// <para>
/// What is asserted here is the <b>order of the statements</b>, because that is the whole
/// correctness argument and it cannot be checked any other way in this suite. The counterparty
/// orders are identifiable only through the trades, so they have to be captured before those
/// trades are deleted; and they cannot be deleted first instead, because <c>Trades</c> holds
/// four foreign keys into <c>Orders</c>. Both mistakes produce working-looking SQL — one leaves
/// the orphan the issue is about, the other fails at runtime on the foreign key. Neither is
/// visible in a diff. The batch itself is exercised by running the simulator, in the same way
/// <see cref="SimulatorOutboxDrainTests"/> leaves its <c>SELECT COUNT(*)</c> to a real run.
/// </para>
/// </summary>
public class SimulatorResetOrphanTests
{
    private const string CounterpartyCapture = "INSERT INTO @Counterparty";
    private const string TradesDelete = "DELETE FROM Trades";
    private const string CounterpartyDelete = "WHERE Id IN (SELECT Id FROM @Counterparty)";

    private static int PositionOf(string fragment)
    {
        var index = DataReset.OrderDataDeleteSql.IndexOf(fragment, StringComparison.Ordinal);
        Assert.True(index >= 0, $"The reset batch no longer contains '{fragment}'.");
        return index;
    }

    [Fact]
    public void OrderDataDeleteSql_CounterpartyOrders_AreCapturedBeforeTheTradesAreDeleted()
    {
        // The trades are the only thing that identifies the counterparty orders. Delete them
        // first and the dealer's side becomes unreachable — which is exactly how the residue in
        // issue #257 accumulated, one row per simulated fill, permanently.
        Assert.True(PositionOf(CounterpartyCapture) < PositionOf(TradesDelete));
    }

    [Fact]
    public void OrderDataDeleteSql_CounterpartyOrders_AreDeletedAfterTheTradesAreDeleted()
    {
        // The reverse of the mistake above, and the reason the fix cannot simply be reordered:
        // Trades has four foreign keys into Orders, all NO ACTION, so an order still referenced
        // by a trade cannot be removed. Capture, delete the trades, then delete the orders.
        Assert.True(PositionOf(TradesDelete) < PositionOf(CounterpartyDelete));
    }

    [Fact]
    public void OrderDataDeleteSql_CounterpartyDelete_SkipsOrdersASurvivingTradeStillReferences()
    {
        var delete = DataReset.OrderDataDeleteSql[PositionOf(CounterpartyDelete)..];

        // An order filled against both a simulated and a real user keeps the real trade, so it
        // must survive. Without this clause the delete does not merely over-reach — it fails on
        // FK_Trades_Orders_SellOrderId and the reset throws.
        Assert.Contains("NOT EXISTS", delete, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BuyOrderId")]
    [InlineData("SellOrderId")]
    [InlineData("MakerOrderId")]
    [InlineData("TakerOrderId")]
    public void OrderDataDeleteSql_CounterpartyCapture_MatchesEveryOrderForeignKey(string column)
    {
        // Sliced to the end of its own statement, not to the next one, so this stays a check on
        // the capture's columns even when the statement order is what has been broken.
        var start = PositionOf(CounterpartyCapture);
        var capture = DataReset.OrderDataDeleteSql[start..DataReset.OrderDataDeleteSql.IndexOf(';', start)];

        // Each of the four is its own foreign key, so any of them can hold an order in place.
        Assert.Contains($"t.{column} = o.Id", capture, StringComparison.Ordinal);
    }
}
