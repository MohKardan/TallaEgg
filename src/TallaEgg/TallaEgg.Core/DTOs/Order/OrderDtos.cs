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
        public decimal Price { get; set; }
        public OrderType Type { get; set; }
        public OrderStatus Status { get; set; }
        public TradingType TradingType { get; set; }
        public OrderRole Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Notes { get; set; }
    }
    /// <summary>
    /// Unified order creation request for all order types
    /// درخواست واحد ایجاد سفارش برای تمام انواع سفارشات
    /// </summary>
    public class OrderDto
    {
        /// <summary>
        /// شناسه کاربر
        /// </summary>
        [Required(ErrorMessage = "شناسه کاربر الزامی است")]
        public Guid Id { get; set; }

        [JsonPropertyName("symbol")]
        public string Asset { get; set; } = "";
        /// <summary>
        /// نماد دارایی (مثل BTC، ETH)
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
        /// مقدار سفارش
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
        /// قیمت (برای سفارشات محدود - اختیاری برای سفارشات بازار)
        /// </summary>
        public decimal Price { get; set; }
        public Guid UserId { get; set; }

        /// <summary>
        /// نوع سفارش: خرید یا فروش
        /// public OrderType Side { get; set; }
        /// شاید ساید درست تر باشد چون بعدا اگر قصد داشته باشیم سفارشات با حد سود و ضرر اضافه کنیم شاید مفهوم اشتباهی برسونه
        /// </summary>
        [Required(ErrorMessage = "نوع سفارش الزامی است")]
        //public OrderType Side { get; set; }
        public OrderType Type { get; set; }
        public OrderStatus Status { get; set; }
        public TradingType TradingType { get; set; }
        public OrderRole Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Notes { get; set; }
        public Guid? ParentOrderId { get; set; } // برای Taker orders که به Maker order متصل می‌شوند
    }
    
}
