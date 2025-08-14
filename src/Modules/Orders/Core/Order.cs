using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Api.Modules.Orders.Core;

public class Order
{
    public Guid Id { get; set; }
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public Guid UserId { get; set; }
    public OrderType Type { get; set; }
    public OrderStatus Status { get; set; }
    public TradingType TradingType { get; set; }
    public OrderRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Notes { get; set; }
    public Guid? ParentOrderId { get; set; }
    public decimal? ExecutedAmount { get; set; }
    public decimal? ExecutedPrice { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? OrderId { get; set; } // External order ID
}
