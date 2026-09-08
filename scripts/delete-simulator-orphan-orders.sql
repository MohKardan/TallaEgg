/*
    Delete the orphan orders left behind by the simulator's reset — issue #257
    --------------------------------------------------------------------------
    Why: until the fix in DataReset.cs, the simulator's reset deleted a fill's trade and the
    simulated user's order but not the counterparty's, because the counterparty is the market
    maker — a real account, and so never in the "TelegramId < 0" list the delete is scoped to.
    Each such fill left one order behind permanently: 4,449 of 4,496 orders on one developer
    machine were that residue.

    It is not merely untidy. A Completed order with RemainingAmount = 0 and no trade on either
    side is a state the product cannot produce, and it silently corrupts anything counted from
    the Orders table — issue #250 opened by measuring 4,523 orders that were almost entirely
    this.

    What this deletes: exactly that impossible state, and nothing else. An order with no trade
    that is Cancelled or Failed is left alone — it never traded, which is legitimate and is what
    those statuses mean.

    Scope: developer databases only. Production never runs the simulator and so has none of
    this. The fix in DataReset.cs stops new residue accumulating; this clears what is already
    there, once.

    Run:
        sqlcmd -S "localhost\SQLEXPRESS" -E -C -i scripts\delete-simulator-orphan-orders.sql

    Stop the services first, so nothing writes an order mid-script. Re-running is harmless:
    the second run finds nothing to do.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

USE TallaEggOrders;
GO

PRINT '=== Orders database: TallaEggOrders ===';

DECLARE @total int = (SELECT COUNT(*) FROM Orders);
DECLARE @target int = (
    SELECT COUNT(*) FROM Orders o
    WHERE o.Status = 'Completed'
      AND o.RemainingAmount = 0
      AND NOT EXISTS (
          SELECT 1 FROM Trades t
          WHERE t.BuyOrderId = o.Id OR t.SellOrderId = o.Id
             OR t.MakerOrderId = o.Id OR t.TakerOrderId = o.Id));

PRINT '  Orders before:            ' + CAST(@total AS varchar(10));
PRINT '  Completed, no trade:      ' + CAST(@target AS varchar(10));

BEGIN TRANSACTION;

    -- All four columns are matched, not just buy and sell: each is its own foreign key into
    -- Orders, so any of them referencing a row means that row is still part of a trade.
    DELETE FROM Orders
    WHERE Status = 'Completed'
      AND RemainingAmount = 0
      AND NOT EXISTS (
          SELECT 1 FROM Trades t
          WHERE t.BuyOrderId = Orders.Id OR t.SellOrderId = Orders.Id
             OR t.MakerOrderId = Orders.Id OR t.TakerOrderId = Orders.Id);

    PRINT '  Deleted:                  ' + CAST(@@ROWCOUNT AS varchar(10));

COMMIT TRANSACTION;
GO
DECLARE @after int = (SELECT COUNT(*) FROM Orders);
DECLARE @trades int = (SELECT COUNT(*) FROM Trades);

PRINT '  Orders after:             ' + CAST(@after AS varchar(10));
PRINT '  Trades (unchanged):       ' + CAST(@trades AS varchar(10));
GO
