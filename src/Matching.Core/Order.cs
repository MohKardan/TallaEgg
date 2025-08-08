using System;

namespace Matching.Core;

public class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public OrderType Type { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public decimal? ExecutedAmount { get; set; }
    public decimal? ExecutedPrice { get; set; }
    public string? OrderId { get; set; } // External order ID
}

public enum OrderType
{
    Buy,   // خرید
    Sell   // فروش
}

public enum OrderStatus
{
    Pending,    // در انتظار
    Partial,    // جزئی
    Filled,     // تکمیل شده
    Cancelled,  // لغو شده
    Rejected    // رد شده
}

public class Trade
{
    public Guid Id { get; set; }
    public Guid BuyOrderId { get; set; }
    public Guid SellOrderId { get; set; }
    public Guid BuyerUserId { get; set; }
    public Guid SellerUserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public DateTime ExecutedAt { get; set; }
    public decimal Fee { get; set; }
    public string? TradeId { get; set; } // External trade ID
} 