using TallaEgg.Core.Enums.Order;

namespace Orders.Core;

/// <summary>
/// The admin's published quote for a symbol: the price they buy at and the price they sell at.
///
/// <para>
/// <b>A quote is not an order.</b> That is the whole point of issue #48. The admin used to
/// announce prices by placing two 1000-gram limit orders, which caused five problems: the 1000
/// was arbitrary; roughly 19 billion toman and 1000 grams of collateral were locked purely to
/// "announce a price"; liquidity ran out once that quantity was consumed; and what the admin
/// thought of as "today's quote" the system stored as an "order".
/// </para>
///
/// <para>
/// Publishing a quote locks no collateral and puts nothing in the order book. Orders are created
/// only at the instant a customer accepts the quote — for exactly the requested quantity — and are
/// consumed immediately.
/// </para>
///
/// <para>
/// <b>No quantity cap:</b> the business decision is that if the admin lacks the balance the trade
/// still goes through and their balance goes negative — today's behaviour, since the admin is
/// exempt from the credit check. The risk of that position accumulating is tracked in issue #61.
/// </para>
/// </summary>
public class Quote
{
    public Guid Id { get; private set; }

    /// <summary>Symbol in BASE/QUOTE form, for example MAUA/IRT.</summary>
    public string Symbol { get; private set; } = string.Empty;

    /// <summary>
    /// The price the admin <b>buys</b> at — that is, the price at which the customer <b>sells</b>.
    /// Stored per base unit (toman per gram), not per mesghal; the mesghal conversion happens in
    /// the bot layer, where the user enters and reads the number.
    /// </summary>
    public decimal BuyPrice { get; private set; }

    /// <summary>The price the admin <b>sells</b> at — that is, the price at which the customer <b>buys</b>.</summary>
    public decimal SellPrice { get; private set; }

    /// <summary>The admin who published the quote; the counterparty to every trade filled against it.</summary>
    public Guid PublishedByUserId { get; private set; }

    public DateTime PublishedAt { get; private set; }

    /// <summary>
    /// Only one quote per symbol is active. Publishing a new one deactivates the previous one.
    /// Old quotes are not deleted, so it stays possible to find out what price a past trade used.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTime? DeactivatedAt { get; private set; }

    /// <summary>EF Core requires a parameterless constructor.</summary>
    private Quote() { }

    public static Quote Publish(string symbol, decimal buyPrice, decimal sellPrice, Guid publishedByUserId)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("نماد نمی‌تواند خالی باشد.", nameof(symbol));

        if (buyPrice <= 0)
            throw new ArgumentException("قیمت خرید باید بزرگ‌تر از صفر باشد.", nameof(buyPrice));

        if (sellPrice <= 0)
            throw new ArgumentException("قیمت فروش باید بزرگ‌تر از صفر باشد.", nameof(sellPrice));

        // A negative spread means the admin buys higher than they sell. A customer could buy and
        // sell endlessly, profiting on every round trip straight out of the shop's pocket. Catch
        // it here rather than after a few trades have already run.
        if (buyPrice > sellPrice)
            throw new ArgumentException(
                $"قیمت خرید ({buyPrice}) نمی‌تواند از قیمت فروش ({sellPrice}) بیشتر باشد.");

        if (publishedByUserId == Guid.Empty)
            throw new ArgumentException("شناسهٔ منتشرکننده الزامی است.", nameof(publishedByUserId));

        return new Quote
        {
            Id = Guid.NewGuid(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            PublishedByUserId = publishedByUserId,
            PublishedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The price the customer trades at.
    ///
    /// Deliberately on the entity rather than in a service: swapping these two is the buyer/seller
    /// inversion bug wearing a different hat. A customer who buys, buys from the admin, and so
    /// pays the admin's sell price.
    /// </summary>
    public decimal PriceFor(OrderSide customerSide) =>
        customerSide == OrderSide.Buy ? SellPrice : BuyPrice;
}
