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
    /// A quote the plausibility band held back until an admin says whether it is a real price
    /// (issue #158). Nothing here is tradeable: no row reaches the Quotes table until it is
    /// approved.
    /// </summary>
    public class PendingQuoteDto
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; } = string.Empty;

        /// <summary>The price the shop would buy at, if this is approved.</summary>
        public decimal BuyPrice { get; set; }

        /// <summary>The price the shop would sell at, if this is approved.</summary>
        public decimal SellPrice { get; set; }

        /// <summary>The midpoint of the two prices above — what the band actually measured.</summary>
        public decimal ProposedMid { get; set; }

        /// <summary>The mid it was compared against; null on a symbol that has never had a quote.</summary>
        public decimal? PreviousMid { get; set; }

        /// <summary>How far the proposal sits from <see cref="PreviousMid"/>, as a percentage.</summary>
        public decimal DeviationPercent { get; set; }

        /// <summary>The band it crossed, so the message can quote the rule as well as the breach.</summary>
        public decimal BandPercent { get; set; }

        /// <summary>"Auto" for the price feed, "Manual" for a price an admin typed.</summary>
        public string Source { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        /// <summary>When the proposal stops being publishable. Past this it can only be discarded.</summary>
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>An admin approving or rejecting a held quote.</summary>
    public record ResolvePendingQuoteRequest(Guid AdminUserId);

    /// <summary>
    /// The answer to <c>POST /api/quotes</c>. A quote inside the band is published immediately and
    /// <see cref="Published"/> carries it; one outside is held and <see cref="Pending"/> carries the
    /// proposal instead, so the caller can put the question to whoever is at the keyboard.
    /// </summary>
    public class PublishQuoteResult
    {
        public QuoteDto? Published { get; set; }
        public PendingQuoteDto? Pending { get; set; }

        /// <summary>True when the quote is live; false when somebody has to answer first.</summary>
        public bool NeedsApproval => Pending is not null;
    }

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
