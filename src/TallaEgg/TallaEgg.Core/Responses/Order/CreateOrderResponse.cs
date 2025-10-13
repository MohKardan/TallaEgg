using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.Responses.Order;

/// <summary>
/// پاسخ ایجاد سفارش واحد
/// </summary>
public class CreateOrderResponse
{
    /// <summary>
    /// اطلاعات سفارش ایجاد شده
    /// </summary>
    public OrderHistoryDto Order { get; set; } = null!;

    /// <summary>
    /// معاملات اجرا شده (در صورت تطبیق فوری)
    /// </summary>
    public List<TradeDto> ExecutedTrades { get; set; } = new();

    /// <summary>
    /// نقش سفارش: Maker، Taker یا Mixed
    /// </summary>
    public OrderRole Role { get; set; }

    /// <summary>
    /// پیام توضیحی برای کاربر
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// آیا سفارش فوری اجرا شد؟
    /// </summary>
    public bool IsExecutedImmediately => ExecutedTrades.Any();

    /// <summary>
    /// مقدار اجرا شده
    /// </summary>
    public decimal ExecutedQuantity => ExecutedTrades.Sum(t => t.Quantity);

    /// <summary>
    /// مقدار باقی‌مانده در Order Book
    /// </summary>
    public decimal RemainingQuantity => Order.Amount - ExecutedQuantity;

    /// <summary>
    /// درصد اجرا شده
    /// </summary>
    public decimal ExecutionPercentage => Order.Amount > 0 ? (ExecutedQuantity / Order.Amount) * 100 : 0;

    /// <summary>
    /// متوسط قیمت اجرا شده
    /// </summary>
    public decimal AverageExecutedPrice => ExecutedTrades.Any() 
        ? ExecutedTrades.Sum(t => t.Price * t.Quantity) / ExecutedTrades.Sum(t => t.Quantity) 
        : 0;

    /// <summary>
    /// کل کارمزد پرداخت شده
    /// </summary>
    public decimal TotalFeesPaid => ExecutedTrades.Sum(t => t.FeeBuyer + t.FeeSeller);
}
