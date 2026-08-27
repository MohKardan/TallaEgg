using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.DTOs.Order
{
    // Mapping from the Quote entity is deliberately not here: this DTO is shared between services
    // and TallaEgg.Core must not depend on the Orders service's domain model. The mapping lives in
    // the layer that knows both.

    /// <summary>
    /// The admin's published quote, for transport between the services and the bot.
    ///
    /// Prices are per base unit, toman per gram. Conversion to mesghal happens only in the bot layer
    /// at display time, where the user enters and reads the number. Spreading the conversion across
    /// layers recreates the ambiguity that previously made it unclear whether a figure was per gram
    /// or per mesghal.
    /// </summary>
    public class QuoteDto
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; } = string.Empty;

        /// <summary>The price the admin buys at — the price at which the customer sells.</summary>
        public decimal BuyPrice { get; set; }

        /// <summary>The price the admin sells at — the price at which the customer buys.</summary>
        public decimal SellPrice { get; set; }

        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Whether this is the quote customers are currently trading on. False for a quote
        /// that has been replaced by a newer one.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>When a newer quote replaced this one; null while it is still active.</summary>
        public DateTime? DeactivatedAt { get; set; }
    }

    /// <summary>An admin's request to publish a quote. Prices are per base unit, toman per gram.</summary>
    public record PublishQuoteRequest(
        string Symbol,
        decimal BuyPrice,
        decimal SellPrice,
        Guid PublishedByUserId);

    /// <summary>
    /// A customer's request to fill a quote.
    ///
    /// Deliberately carries no price: the price is read from the published quote. If the customer
    /// sent one, it would have to be validated against the quote — an extra rule that opens a way to
    /// get it wrong.
    /// </summary>
    public record AcceptQuoteRequest(
        Guid UserId,
        string Symbol,
        OrderSide Side,
        decimal Quantity);
}
