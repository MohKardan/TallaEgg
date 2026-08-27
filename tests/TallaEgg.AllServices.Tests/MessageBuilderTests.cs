using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.TelegramBot.Infrastructure.Messages;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The three message builders extracted from <c>BotHandler</c> and <c>BotHandlerAdmin</c>
/// in issue #65.
///
/// Every defect this bot has shipped in a customer-visible message compiled cleanly and
/// looked plausible in Telegram — a Persian date wrong by 22 days, a trade history that
/// did not say buy or sell, times in UTC. They were found by a person reading a message,
/// which is not a repeatable process. These tests make the text assertable without a
/// Telegram connection.
///
/// The specific hazard being pinned here is the mesghal conversion. Gold prices are stored
/// per gram and shown per mesghal, a factor of 4.3318. Getting it backwards, or applying
/// it twice, or applying it to the total as well as the unit price, produces a number that
/// is wrong by 4.3x and breaks nothing.
/// </summary>
public class MessageBuilderTests
{
    private const string Gold = CurrenciesConstant.MAUA_IRT; // MAUA/IRT
    private const string Crypto = CurrenciesConstant.BTC_IRT;

    /// <summary>
    /// 80,000,000 toman per mesghal is 18,468,073.32 per gram. Used throughout so the two
    /// units are always recognisable in an assertion.
    /// </summary>
    private const decimal PricePerGram = 18_468_073.32m;
    private const decimal PricePerMesghal = 80_000_000m;

    /// <summary>Digits as the customer sees them; the templates render Persian numerals.</summary>
    private static string Fa(decimal value) => PersianFormatFor(value);
    private static string PersianFormatFor(decimal value) =>
        TallaEgg.Core.Utilties.PersianFormat.Number(value);

    // ── TradeExecutedMessage ────────────────────────────────────────────────────

    /// <summary>
    /// A buy and a sell move money in opposite directions. The template reuses one slot for
    /// both, so the label is the only thing telling the customer which happened — and it is
    /// exactly the kind of detail that was already wrong once in the trade history.
    /// </summary>
    [Fact]
    public void TradeExecuted_ABuyIsLabelledAsMoneyPaid()
    {
        var text = TradeExecutedMessage.Build(Gold, OrderSide.Buy, 2.5m, PricePerGram);

        Assert.Contains("پرداختی", text);
        Assert.DoesNotContain("دریافتی", text);
    }

    [Fact]
    public void TradeExecuted_ASellIsLabelledAsMoneyReceived()
    {
        var text = TradeExecutedMessage.Build(Gold, OrderSide.Sell, 2.5m, PricePerGram);

        Assert.Contains("دریافتی", text);
        Assert.DoesNotContain("پرداختی", text);
    }

    /// <summary>
    /// Gold is quoted per mesghal. Showing the stored per-gram number would tell the
    /// customer they traded at 18.4 million when they traded at 80 million.
    /// </summary>
    [Fact]
    public void TradeExecuted_ForGold_ShowsThePricePerMesghal()
    {
        var text = TradeExecutedMessage.Build(Gold, OrderSide.Buy, 2.5m, PricePerGram);

        Assert.Contains("قیمت هر مثقال", text);
        Assert.Contains(Fa(PricePerMesghal), text);
    }

    /// <summary>
    /// The total must come from the per-gram price. Multiplying the quantity by the
    /// displayed per-mesghal price instead overstates it by 4.3318x — and both numbers look
    /// entirely reasonable on their own.
    /// </summary>
    [Fact]
    public void TradeExecuted_TotalIsQuantityTimesThePerGramPrice_NotTheDisplayedPrice()
    {
        var text = TradeExecutedMessage.Build(Gold, OrderSide.Buy, 2.5m, PricePerGram);

        var expected = CurrenciesConstant.RoundToCurrencyPrecision(
            2.5m * PricePerGram, CurrenciesConstant.Toman);

        Assert.Contains(Fa(expected), text);
        Assert.DoesNotContain(Fa(2.5m * PricePerMesghal), text);
    }

    /// <summary>Non-gold pairs have no mesghal, so the price is shown as stored.</summary>
    [Fact]
    public void TradeExecuted_ForANonGoldPair_ShowsThePriceUnconverted()
    {
        var text = TradeExecutedMessage.Build(Crypto, OrderSide.Buy, 2m, 1_000m);

        Assert.Contains("قیمت هر واحد", text);
        Assert.DoesNotContain("مثقال", text);
        Assert.Contains(Fa(1_000m), text);
    }

    // ── OrderConfirmationMessage ────────────────────────────────────────────────

    /// <summary>
    /// The gold template takes six arguments and the non-gold one takes five. Passing the
    /// wrong count is not a compile error: it either throws in front of the customer or
    /// leaves a literal placeholder in the message. Asserting no brace survives covers the
    /// whole family at once.
    /// </summary>
    [Theory]
    [InlineData(Gold)]
    [InlineData(Crypto)]
    public void OrderConfirmation_LeavesNoUnfilledPlaceholder(string symbol)
    {
        var text = OrderConfirmationMessage.Build(symbol, OrderSide.Buy, 2.5m, PricePerGram);

        Assert.DoesNotContain("{", text);
        Assert.DoesNotContain("}", text);
    }

    /// <summary>
    /// Gold shows both units. Only one of them makes the number ambiguous: read as the
    /// wrong unit it is off by 4.3318x, which is large enough to be a different market and
    /// small enough not to look like a bug.
    /// </summary>
    [Fact]
    public void OrderConfirmation_ForGold_ShowsBothThePerMesghalAndPerGramPrice()
    {
        var text = OrderConfirmationMessage.Build(Gold, OrderSide.Buy, 2.5m, PricePerGram);

        Assert.Contains("قیمت هر مثقال", text);
        Assert.Contains("قیمت هر گرم", text);
        Assert.Contains(Fa(PricePerMesghal), text);
        Assert.Contains(Fa(PricePerGram), text);
    }

    /// <summary>
    /// The limit path shows back the number the customer typed rather than deriving it from
    /// the stored price. The two differ because dividing by 4.3318 and multiplying back does
    /// not round-trip, and a confirmation quoting a slightly different figure than the one
    /// just entered reads as the bot having mis-recorded the order.
    /// </summary>
    [Fact]
    public void OrderConfirmation_ShowsTheCustomersOwnEnteredPrice_NotAReDerivedOne()
    {
        const decimal entered = 80_000_001m; // deliberately not a clean round-trip
        var storedPerGram = entered / CurrenciesConstant.GramsPerMesghal;

        var text = OrderConfirmationMessage.Build(
            Gold, OrderSide.Buy, 2.5m, storedPerGram, displayPricePerMesghal: entered);

        Assert.Contains(Fa(entered), text);
    }

    /// <summary>
    /// Without an override the per-mesghal figure is derived. This is the dealer path, where
    /// the quote is the source of the price and there is no customer input to preserve.
    /// </summary>
    [Fact]
    public void OrderConfirmation_WithoutAnOverride_DerivesThePerMesghalPrice()
    {
        var text = OrderConfirmationMessage.Build(Gold, OrderSide.Buy, 2.5m, PricePerGram);

        Assert.Contains(Fa(PricePerGram * CurrenciesConstant.GramsPerMesghal), text);
    }

    /// <summary>A non-gold pair has no second unit, so the override is meaningless and ignored.</summary>
    [Fact]
    public void OrderConfirmation_ForANonGoldPair_IgnoresTheMesghalOverride()
    {
        var text = OrderConfirmationMessage.Build(
            Crypto, OrderSide.Buy, 2m, 1_000m, displayPricePerMesghal: 999_999m);

        Assert.DoesNotContain("مثقال", text);
        Assert.DoesNotContain(Fa(999_999m), text);
    }

    [Theory]
    [InlineData(OrderSide.Buy, "خرید")]
    [InlineData(OrderSide.Sell, "فروش")]
    public void OrderConfirmation_NamesTheSide(OrderSide side, string expected)
    {
        var text = OrderConfirmationMessage.Build(Gold, side, 2.5m, PricePerGram);

        Assert.Contains(expected, text);
    }

    // ── QuoteMessage ────────────────────────────────────────────────────────────

    /// <summary>
    /// The admin types prices per mesghal; quotes are stored per gram. Publishing the typed
    /// number unconverted would price every customer trade 4.3318x too high.
    /// </summary>
    [Fact]
    public void Quote_ConvertsTheAdminsMesghalPricesToPerGram()
    {
        var quote = QuoteMessage.Prepare(Gold, buyPrice: 79_000_000m, sellPrice: 80_000_000m);

        Assert.Equal(
            CurrenciesConstant.RoundOrderPrice(79_000_000m / CurrenciesConstant.GramsPerMesghal),
            quote.BuyPricePerGram);
        Assert.Equal(
            CurrenciesConstant.RoundOrderPrice(80_000_000m / CurrenciesConstant.GramsPerMesghal),
            quote.SellPricePerGram);
    }

    /// <summary>
    /// The whole reason conversion and text are produced together: the numbers published
    /// must be the numbers displayed. If they could drift, every customer would trade at a
    /// price the shop owner never saw.
    /// </summary>
    [Fact]
    public void Quote_PublishesExactlyThePricesItDisplays()
    {
        var quote = QuoteMessage.Prepare(Gold, 79_000_000m, 80_000_000m);

        Assert.Contains(Fa(quote.BuyPricePerGram), quote.Text);
        Assert.Contains(Fa(quote.SellPricePerGram), quote.Text);
    }

    /// <summary>Both units appear, so the admin can sanity-check the conversion themselves.</summary>
    [Fact]
    public void Quote_ShowsTheTypedMesghalPricesAlongsideThePerGramOnes()
    {
        var quote = QuoteMessage.Prepare(Gold, 79_000_000m, 80_000_000m);

        Assert.Contains(Fa(79_000_000m), quote.Text);
        Assert.Contains(Fa(80_000_000m), quote.Text);
        Assert.DoesNotContain("{", quote.Text);
    }

    /// <summary>
    /// Buy and sell must not be swapped. An inverted spread means the shop buys higher than
    /// it sells, and a customer could cycle buy/sell indefinitely at the shop's expense.
    /// The Orders service rejects that (QuoteTests), but the bot must not be the thing that
    /// creates it.
    /// </summary>
    [Fact]
    public void Quote_KeepsTheBuyPriceBelowTheSellPrice()
    {
        var quote = QuoteMessage.Prepare(Gold, buyPrice: 79_000_000m, sellPrice: 80_000_000m);

        Assert.True(quote.BuyPricePerGram < quote.SellPricePerGram,
            $"buy {quote.BuyPricePerGram} should be below sell {quote.SellPricePerGram}");
    }

    /// <summary>
    /// MAUA/IRT is the only symbol with a mesghal/gram duality. For every other symbol what
    /// the admin typed already is the per-unit price — dividing it by 4.3318 would misprice
    /// every trade on that symbol by the same factor the mesghal conversion exists to avoid.
    /// </summary>
    [Fact]
    public void Quote_ForANonGoldSymbol_PublishesThePriceUnconverted()
    {
        var quote = QuoteMessage.Prepare(Crypto, buyPrice: 3_000_000_000m, sellPrice: 3_010_000_000m);

        Assert.Equal(3_000_000_000m, quote.BuyPricePerGram);
        Assert.Equal(3_010_000_000m, quote.SellPricePerGram);
    }

    [Fact]
    public void Quote_ForANonGoldSymbol_TextShowsThePriceOnceNotTwice()
    {
        var quote = QuoteMessage.Prepare(Crypto, buyPrice: 3_000_000_000m, sellPrice: 3_010_000_000m);

        Assert.Contains(Fa(3_000_000_000m), quote.Text);
        Assert.Contains(Fa(3_010_000_000m), quote.Text);
        Assert.DoesNotContain("{", quote.Text);
        // No mesghal/gram duality for this symbol, so the mesghal-specific label must not
        // leak into a message about it.
        Assert.DoesNotContain("مثقال", quote.Text);
    }
}
