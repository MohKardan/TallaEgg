using TallaEgg.Core.Enums.Order;

namespace TallaEgg.TelegramBot.Infrastructure.Conversations;

/// <summary>
/// What the customer has chosen so far while placing an order.
///
/// Moved out of <c>BotHandler.cs</c> in issue #65: it is the conversation's data, not the
/// handler's, and it is now reachable from <see cref="IConversationStore"/> without
/// dragging the handler along.
/// </summary>
public class OrderState
{
    public OrderType OrderType { get; set; }
    public TradingType TradingType { get; set; }
    public OrderSide OrderSide { get; set; }

    /// <summary>Trading pair, e.g. <c>MAUA/IRT</c>.</summary>
    public string Asset { get; set; } = "";

    public decimal Amount { get; set; }

    /// <summary>
    /// Price in the unit the system stores, which for gold is per gram — not the per-mesghal
    /// figure the customer typed. Converting on the way in keeps a single unit everywhere
    /// past this point.
    /// </summary>
    public decimal Price { get; set; }

    public decimal? BestBidPrice { get; set; }
    public decimal? BestAskPrice { get; set; }
    public Guid UserId { get; set; }
    public bool IsConfirmed { get; set; } = false;
    public string? Notes { get; set; } = null;

    /// <summary>
    /// Which step the conversation is waiting on, e.g. <c>waiting_for_amount</c>. Empty
    /// means no step is pending.
    /// </summary>
    public string State { get; set; } = "";
}
