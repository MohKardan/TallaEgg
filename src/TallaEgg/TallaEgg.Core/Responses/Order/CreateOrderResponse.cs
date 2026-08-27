using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.Responses.Order;

/// <summary>
/// Response to a single order-creation request.
/// </summary>
public class CreateOrderResponse
{
    /// <summary>
    /// The created order.
    /// </summary>
    public OrderHistoryDto Order { get; set; } = null!;

    /// <summary>
    /// Trades executed, if the order matched immediately.
    /// </summary>
    public List<TradeDto> ExecutedTrades { get; set; } = new();

    /// <summary>
    /// The order's role: maker, taker or mixed.
    /// </summary>
    public OrderRole Role { get; set; }

    /// <summary>
    /// A message for the user.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether the order executed immediately.
    /// </summary>
    public bool IsExecutedImmediately => ExecutedTrades.Any();

    /// <summary>
    /// Quantity executed.
    /// </summary>
    public decimal ExecutedQuantity => ExecutedTrades.Sum(t => t.Quantity);

    /// <summary>
    /// Quantity still resting in the order book.
    /// </summary>
    public decimal RemainingQuantity => Order.Amount - ExecutedQuantity;

    /// <summary>
    /// Percentage executed.
    /// </summary>
    public decimal ExecutionPercentage => Order.Amount > 0 ? (ExecutedQuantity / Order.Amount) * 100 : 0;

    /// <summary>
    /// Average execution price.
    /// </summary>
    public decimal AverageExecutedPrice => ExecutedTrades.Any() 
        ? ExecutedTrades.Sum(t => t.Price * t.Quantity) / ExecutedTrades.Sum(t => t.Quantity) 
        : 0;

    /// <summary>
    /// Total fees paid.
    /// </summary>
    public decimal TotalFeesPaid => ExecutedTrades.Sum(t => t.FeeBuyer + t.FeeSeller);
}
