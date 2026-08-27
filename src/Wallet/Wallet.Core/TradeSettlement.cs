namespace Wallet.Core;

/// <summary>
/// One row per settled trade, keyed on <see cref="TradeId"/> itself.
///
/// Why this table exists:
/// "every trade settles exactly once" used to be guaranteed by a single SELECT in code that ran
/// outside the transaction and had no backing in the database. Two concurrent settlements of the
/// same trade could both pass that check and both apply, creating money from nothing (issue #42).
///
/// Making TradeId the primary key turns the guarantee from a rule in code into a fact in the
/// schema: even if some future path bypasses the check, the database refuses the second insert.
/// That is the lesson from #53 — a structural guard outlasts a behavioural one.
///
/// Why a separate table rather than a unique index on Transactions:
/// each settlement correctly writes four transaction rows sharing one ReferenceId, one per leg, so
/// the unique index would have to be on (WalletId, ReferenceId). That works but does not state the
/// intent. Here "one row = one settlement" reads directly.
/// </summary>
public class TradeSettlement
{
    /// <summary>Trade id from the Orders service. Primary key — this is what makes a duplicate settlement impossible.</summary>
    public Guid TradeId { get; private set; }

    /// <summary>When the settlement was applied. Used for reconciliation and to answer "when did this trade settle?".</summary>
    public DateTime SettledAt { get; private set; }

    /// <summary>
    /// Symbol and amounts are kept for audit. Without them, working out what a settlement row refers
    /// to requires joining against the Orders service, which is a different database.
    /// </summary>
    public string Symbol { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal QuoteQuantity { get; private set; }

    public Guid BuyerUserId { get; private set; }

    public Guid SellerUserId { get; private set; }

    /// <summary>EF Core requires a parameterless constructor.</summary>
    private TradeSettlement() { }

    public static TradeSettlement Create(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity)
    {
        return new TradeSettlement
        {
            TradeId = tradeId,
            BuyerUserId = buyerUserId,
            SellerUserId = sellerUserId,
            Symbol = symbol,
            Quantity = quantity,
            QuoteQuantity = quoteQuantity,
            SettledAt = DateTime.UtcNow
        };
    }
}
