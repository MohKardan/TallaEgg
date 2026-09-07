using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.DTOs.Order
{
    public class OrderHistoryDto
    {
        public Guid Id { get; set; }
        public string Asset { get; set; } = string.Empty;
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
    /// <remarks>
    /// One value, one wire name (issue #235). This class used to carry the repository's only four
    /// <c>[JsonPropertyName]</c> attributes and all four were crossed: <c>Asset</c> serialized as
    /// <c>"symbol"</c> while an alias called <c>Symbol</c> serialized as <c>"asset"</c>, and the
    /// same for <c>Amount</c>/<c>Quantity</c>. Two values reached the wire as four fields, and on
    /// the way in the last key won, so the accepted quantity depended on the order of the keys in
    /// the request body.
    ///
    /// The aliases went with the attributes rather than surviving them: kept as plain properties
    /// they would have produced the same four fields again under camelCase names, which is the
    /// defect, not a smaller version of it. <c>Asset</c> and <c>Amount</c> are the survivors
    /// because this endpoint's own response uses those names (<see cref="OrderHistoryDto"/>), so
    /// request and response now agree.
    ///
    /// The DataAnnotations went too. Minimal APIs do not execute them — nothing in this repository
    /// calls <c>Validator.TryValidateObject</c> or registers a validation filter — so they enforced
    /// nothing while making the generated schema advertise constraints the server does not apply.
    /// Orders.Api validates this request by hand instead.
    /// </remarks>
    public class OrderDto
    {
        /// <summary>
        /// User id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Asset symbol, as a trading pair — for example <c>MAUA/IRT</c>.
        /// </summary>
        public string Asset { get; set; } = "";
        /// <summary>
        /// Order quantity, in the base asset.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Price. Required for limit orders, optional for market orders.
        /// </summary>
        public decimal Price { get; set; }
        public Guid UserId { get; set; }

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
