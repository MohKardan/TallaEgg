using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.DTOs.Order
{
    public class OrderHistoryDto
    {
        public Guid Id { get; set; }
        public string Asset { get; set; }
        public decimal Amount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal Price { get; set; }
        public OrderSide Type { get; set; }
        public OrderStatus Status { get; set; }
        public TradingType TradingType { get; set; }
        public OrderRole Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Notes { get; set; }
        public Guid? ParentOrderId { get; set; }
    }
    /// <summary>
    /// Unified order creation request for all order types
    /// A single order-creation request covering every order type.
    /// </summary>
    public class OrderDto
    {
        /// <summary>
        /// User id.
        /// </summary>
        [Required(ErrorMessage = "شناسه کاربر الزامی است")]
        public Guid Id { get; set; }

        [JsonPropertyName("symbol")]
        public string Asset { get; set; } = "";
        /// <summary>
        /// Asset symbol.
        /// alias
        /// </summary>
        [Required(ErrorMessage = "نماد دارایی الزامی است")]
        [StringLength(20, ErrorMessage = "نماد دارایی نمی‌تواند بیش از 20 کاراکتر باشد")]
        [JsonPropertyName("asset")]
        public string Symbol
        {
            get => Asset;
            set => Asset = value;
        }

        /// <summary>
        /// Order quantity.
        /// alias
        /// </summary>
        [Required(ErrorMessage = "مقدار سفارش الزامی است")]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "مقدار سفارش باید بزرگتر از صفر باشد")]
        [JsonPropertyName("Amount")]
        public decimal Quantity
        {
            get => Amount;
            set => Amount = value;
        }
        [JsonPropertyName("quantity")]
        public decimal Amount { get; set; }
        /// <summary>
        /// Price. Required for limit orders, optional for market orders.
        /// </summary>
        public decimal Price { get; set; }
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "سمت سفارش یا جهت سفارش (خرید یا فروش(")]
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public OrderStatus Status { get; set; }
        public TradingType TradingType { get; set; }
        public OrderRole Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Notes { get; set; }
        public Guid? ParentOrderId { get; set; } // برای Taker orders که به Maker order متصل می‌شوند
    }
    public class BestPricesDto
    {
        public TradingType TradingType { get; set; }
        public OrderType OrderType { get; set; }
        public string Symbol { get; set; } = string.Empty;
        /// <summary>
        /// Best bid: the highest price buyers are offering.
        /// </summary>
        public decimal? BestBidPrice { get; set; }    // بهترین قیمت خرید (بالاترین قیمت پیشنهادی خریداران)
        /// <summary>
        /// Best ask: the lowest price sellers are offering.
        /// </summary>
        public decimal? BestAskPrice { get; set; }    // بهترین قیمت فروش (پایین‌ترین قیمت پیشنهادی فروشندگان)
        public decimal? BidVolume { get; set; }       // حجم موجود در بهترین قیمت خرید
        public decimal? AskVolume { get; set; }       // حجم موجود در بهترین قیمت فروش
        public decimal? Spread { get; set; }          // اختلاف قیمت (Ask - Bid)
        public DateTime Timestamp { get; set; }      // زمان آخرین بروزرسانی
    }

    /// <summary>
    /// Response DTO for canceling active orders
    /// Response DTO for cancelling active orders.
    /// </summary>
    public class CancelActiveOrdersResponseDto
    {
        /// <summary>
        /// Number of orders that were cancelled
        /// How many orders were cancelled.
        /// </summary>
        public int CancelledCount { get; set; }
    }

    /// <summary>
    /// DTO for displaying a user's trade history.
    /// </summary>
    public class TradeHistoryDto
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal QuoteQuantity { get; set; }
        public Guid BuyerUserId { get; set; }
        public Guid SellerUserId { get; set; }
        public Guid MakerUserId { get; set; }
        public Guid TakerUserId { get; set; }
        public decimal FeeBuyer { get; set; }
        public decimal FeeSeller { get; set; }
        public decimal MakerFee { get; set; }
        public decimal TakerFee { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

}
