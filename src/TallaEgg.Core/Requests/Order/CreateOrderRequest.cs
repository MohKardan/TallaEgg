using System.ComponentModel.DataAnnotations;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.Requests.Order;

/// <summary>
/// Unified order creation request for all order types
/// درخواست واحد ایجاد سفارش برای تمام انواع سفارشات
/// </summary>
public class CreateOrderRequest
{
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    [Required(ErrorMessage = "شناسه کاربر الزامی است")]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// نماد دارایی (مثل BTC، ETH)
    /// </summary>
    [Required(ErrorMessage = "نماد دارایی الزامی است")]
    [StringLength(20, ErrorMessage = "نماد دارایی نمی‌تواند بیش از 20 کاراکتر باشد")]
    public string Symbol { get; set; } = null!;

    /// <summary>
    /// نوع سفارش: خرید یا فروش
    /// </summary>
    [Required(ErrorMessage = "نوع سفارش الزامی است")]
    public OrderType Side { get; set; }

    /// <summary>
    /// نوع سفارش: محدود یا بازار
    /// </summary>
    [Required(ErrorMessage = "نوع سفارش الزامی است")]
    public OrderTypeEnum Type { get; set; }

    /// <summary>
    /// قیمت (برای سفارشات محدود - اختیاری برای سفارشات بازار)
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// مقدار سفارش
    /// </summary>
    [Required(ErrorMessage = "مقدار سفارش الزامی است")]
    [Range(0.00000001, double.MaxValue, ErrorMessage = "مقدار سفارش باید بزرگتر از صفر باشد")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// نوع معاملات (نقدی یا آتی)
    /// </summary>
    public TradingType TradingType { get; set; } = TradingType.Spot;

    /// <summary>
    /// یادداشت اختیاری
    /// </summary>
    [StringLength(500, ErrorMessage = "یادداشت نمی‌تواند بیش از 500 کاراکتر باشد")]
    public string? Notes { get; set; }
}
