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
    ///
    /// An <c>Id</c> property went the same way (issue #237). It was a request field nothing read —
    /// an order's id is assigned by the server — and it was documented as "User id." while a
    /// separate <see cref="UserId"/> sat a few properties further down the same class, so a client
    /// following that comment could reasonably have sent the user's id as <c>id</c> and left the
    /// order's user empty. Removing it costs no caller anything: the bot never set it, and a body
    /// that still carries <c>id</c> is accepted with the member ignored.
    ///
    /// Five more went the same way (issue #240): <c>Status</c>, <c>Role</c>, <c>CreatedAt</c>,
    /// <c>UpdatedAt</c> and <c>ParentOrderId</c>. All five were bound, published in the schema and
    /// discarded — the status and timestamps belong to the entity, the role is computed from the
    /// matching result, and the parent is set by the matching engine when a taker is linked to a
    /// maker. <c>Status</c> and <c>Role</c> were the reason the set was worth removing rather than
    /// documenting: they name real trading concepts, so a client reading the schema could
    /// reasonably believe <c>role</c> chose whether to post as maker or taker. Measured against the
    /// running service, <c>{"role": 1, "status": 3, "parentOrderId": "…"}</c> returned <c>200</c>
    /// and a plain maker order at the entity's own status, with no error and no warning.
    /// </remarks>
    public class OrderDto
    {
        /// <summary>
        /// Asset symbol, as a trading pair — for example <c>MAUA/IRT</c>.
        /// </summary>
        public string Asset { get; set; } = "";
        /// <summary>
        /// Order quantity, in the base asset.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Price, required on every order. The endpoint refuses a body without one, whatever
        /// <see cref="Type"/> says (issue #240).
        /// </summary>
        public decimal Price { get; set; }
        public Guid UserId { get; set; }

        public OrderSide Side { get; set; }

        /// <summary>
        /// How the caller means the price to be arrived at. <b>Recorded, not honoured</b>: this
        /// endpoint builds the same resting order whatever it is sent, so <c>Market</c>,
        /// <c>StopLimit</c> and <c>Oco</c> are all stored as asked and then treated alike.
        ///
        /// <para>
        /// It was previously read into a log line and discarded, so <c>Order.Type</c> held the enum
        /// default on every row ever written (issue #250). It stays on the request rather than
        /// being removed with #240's five: ordinary customers cannot name a price today — only the
        /// dealer publishes quotes — and this becomes their choice when peer-to-peer trading opens.
        /// </para>
        ///
        /// <para>
        /// Refusing the types the endpoint cannot honour would be a behaviour change and is filed
        /// separately. Before writing that refusal, note what the bot actually sends: it reaches
        /// this endpoint only when no quote is active, and the value it posts is whichever button
        /// started the conversation. Today that is <c>Market</c> in practice, because the only
        /// order-entry button on the customer's menu is the quote one — but the <c>Limit</c> button
        /// is commented out of the keyboard rather than removed from the handler, so a replayed or
        /// forwarded button label still posts <c>Limit</c>.
        /// </para>
        /// </summary>
        public OrderType Type { get; set; }
        public TradingType TradingType { get; set; }
        public string? Notes { get; set; }
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
