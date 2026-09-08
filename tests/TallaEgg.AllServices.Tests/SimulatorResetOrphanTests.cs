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
/// What is asserted here is the <b>shape and order of the statements</b>, because that is the
/// whole correctness argument and it cannot be checked any other way in this suite. The
/// counterparty orders are identifiable only through the trades, so they have to be captured
/// before those trades are deleted; and they cannot be deleted first instead, because
/// <c>Trades</c> holds four foreign keys into <c>Orders</c>. Every mistake here produces
/// working-looking SQL — one leaves the orphan the issue is about, one fails at runtime on the
/// foreign key, one quietly deletes a real user's resting order. None is visible in a diff. The
/// batch itself is exercised by running the simulator, in the same way
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

    /// <summary>
    /// One statement of the batch, from <paramref name="fragment"/> to its terminating
    /// semicolon. Slicing to the end of the string instead would let a clause that has moved
    /// onto some later statement keep satisfying an assertion about this one.
    /// </summary>
    private static string StatementAt(string fragment)
    {
        var start = PositionOf(fragment);
        return DataReset.OrderDataDeleteSql[start..DataReset.OrderDataDeleteSql.IndexOf(';', start)];
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
    public void OrderDataDeleteSql_TheDeletes_ShareOneTransaction()
    {
        // The capture stops being valid the moment the trades are gone. A batch that got as far
        // as deleting them and then failed would leave the orders they identified unrecoverable
        // — the permanent orphans this fix exists to remove. The delete-by-owner it replaced had
        // no such window, because UserId stays true however far the batch got.
        Assert.True(PositionOf("BEGIN TRANSACTION") < PositionOf(TradesDelete));
        Assert.True(PositionOf(CounterpartyDelete) < PositionOf("COMMIT TRANSACTION"));
    }

    [Fact]
    public void OrderDataDeleteSql_CounterpartyDelete_SkipsOrdersASurvivingTradeStillReferences()
    {
        var delete = StatementAt(CounterpartyDelete);

        // An order filled against both a simulated and a real user keeps the real trade, so it
        // must survive. Without this clause the delete does not merely over-reach — it fails on
        // FK_Trades_Orders_SellOrderId and the reset throws.
        Assert.Contains("NOT EXISTS", delete, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderDataDeleteSql_CounterpartyDelete_SkipsOrdersTheTradeDidNotConsumeEntirely()
    {
        var delete = StatementAt(CounterpartyDelete);

        // A real user's resting order that a simulated user only partially filled existed before
        // the run and is not the run's to delete. Removing it would also strand its LockedBalance
        // in a real wallet with no order row left to reconcile against. Only an order that exists
        // solely because of a deleted trade goes — which is what a quote fill's dealer side is.
        Assert.Contains("Status = 'Completed'", delete, StringComparison.Ordinal);
        Assert.Contains("RemainingAmount = 0", delete, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BuyOrderId")]
    [InlineData("SellOrderId")]
    [InlineData("MakerOrderId")]
    [InlineData("TakerOrderId")]
    public void OrderDataDeleteSql_CounterpartyCapture_MatchesEveryOrderForeignKey(string column)
    {
        var capture = StatementAt(CounterpartyCapture);

        // Each of the four is its own foreign key, so any of them can hold an order in place.
        Assert.Contains($"t.{column} = o.Id", capture, StringComparison.Ordinal);
    }
}
