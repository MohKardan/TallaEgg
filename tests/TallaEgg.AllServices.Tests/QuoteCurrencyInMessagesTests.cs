using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure.Messages;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Every price the bot shows used to carry the word «تومان», written into the Persian template
/// beside it. That was true of every symbol until XAU/USD (issue #304), and a dollar price under a
/// toman label is not a cosmetic defect — it is the bot telling a customer the wrong thing about
/// the money they are committing.
///
/// <para>
/// These tests are the reason the symbol can be switched on. They assert the label per symbol on
/// each message a customer or an admin actually sees on the ounce's path, and they assert both
/// directions: the ounce says «دلار» and never «تومان», and gold still says «تومان».
/// </para>
/// </summary>
public class QuoteCurrencyInMessagesTests
{
    private const string Toman = "تومان";
    private const string Dollar = "دلار";

    // Real figures: the ounce around $4,318 and melted gold around 23.7 million toman a gram, both
    // as the live feeds returned them on 2026-09-22.
    private const decimal OuncePrice = 4318.55m;
    private const decimal GoldPricePerGram = 23_707_927m;

    [Theory]
    [InlineData(CurrenciesConstant.XAU_USD, Dollar)]
    [InlineData(CurrenciesConstant.MAUA_IRT, Toman)]
    [InlineData(CurrenciesConstant.SEKE_BAHAR_IRT, Toman)]
    [InlineData(CurrenciesConstant.BTC_IRT, Toman)]
    public void QuoteUnit_IsTheSymbolsOwnCurrency(string symbol, string expected)
    {
        Assert.Equal(expected, PersianFormat.QuoteUnit(symbol));
    }

    /// <summary>A malformed symbol must still produce a unit, not an empty gap beside a number.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("MAUA")]
    [InlineData(null)]
    public void QuoteUnit_FallsBackToToman_ForSomethingThatIsNotASymbol(string? symbol)
    {
        Assert.Equal(Toman, PersianFormat.QuoteUnit(symbol));
    }

    /// <summary>
    /// Asserts the currency where it actually matters — immediately after a figure — rather than
    /// anywhere in the message. Found by the review of PR #321: every symbol's Persian name
    /// already contains its currency ("انس جهانی/دلار", "آبشده/تومان") on the دارایی line, so a
    /// bare Contains("دلار") passed even with the currency argument removed entirely.
    /// </summary>
    private static void AssertPricedIn(string text, string symbol, params decimal[] figures)
    {
        var quoteAsset = CurrenciesConstant.QuoteAssetOf(symbol);
        var unit = PersianFormat.QuoteUnit(symbol);

        foreach (var figure in figures)
        {
            var expected = $"{PersianFormat.Amount(figure, quoteAsset)} {unit}";
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }

        var wrongUnit = unit == Toman ? Dollar : Toman;
        Assert.DoesNotContain($" {wrongUnit}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderConfirmation_ForTheOunce_IsPricedInDollars()
    {
        var text = OrderConfirmationMessage.Build(CurrenciesConstant.XAU_USD, OrderSide.Buy, 0.5m, OuncePrice);

        // Price and total, both in dollars: 0.5 × 4318.55 = 2159.275 → 2159.28 at two decimals.
        AssertPricedIn(text, CurrenciesConstant.XAU_USD, OuncePrice, 2159.28m);
    }

    [Fact]
    public void OrderConfirmation_ForGold_IsStillPricedInToman()
    {
        var text = OrderConfirmationMessage.Build(CurrenciesConstant.MAUA_IRT, OrderSide.Buy, 8m, GoldPricePerGram);

        Assert.Contains(Toman, text, StringComparison.Ordinal);
        Assert.DoesNotContain($" {Dollar}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TradeExecuted_ForTheOunce_IsPricedInDollars()
    {
        var text = TradeExecutedMessage.Build(CurrenciesConstant.XAU_USD, OrderSide.Sell, 1.25m, OuncePrice);

        // 1.25 × 4318.55 = 5398.1875 → 5398.19.
        AssertPricedIn(text, CurrenciesConstant.XAU_USD, OuncePrice, 5398.19m);
    }

    [Fact]
    public void TradeExecuted_ForTheCoin_IsStillPricedInToman()
    {
        var text = TradeExecutedMessage.Build(CurrenciesConstant.SEKE_BAHAR_IRT, OrderSide.Sell, 2m, 230_130_000m);

        AssertPricedIn(text, CurrenciesConstant.SEKE_BAHAR_IRT, 230_130_000m, 460_260_000m);
    }

    /// <summary>
    /// The admin's own confirmation after publishing. They are being shown the prices customers
    /// will trade at, so a wrong currency here is wrong before a single customer sees it.
    /// </summary>
    [Fact]
    public void PublishedQuote_ForTheOunce_IsPricedInDollars()
    {
        var text = QuoteMessage.Prepare(CurrenciesConstant.XAU_USD, 4315.20m, 4321.90m).Text;

        AssertPricedIn(text, CurrenciesConstant.XAU_USD, 4315.20m, 4321.90m);
    }

    [Fact]
    public void HeldQuote_ForTheOunce_IsPricedInDollars()
    {
        var (text, _) = PendingQuoteMessage.Build(OuncePending(), new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc));

        AssertPricedIn(text, CurrenciesConstant.XAU_USD, 4315.20m, 4321.90m, 4290.00m);
    }

    [Fact]
    public void ApprovedQuote_ForTheOunce_IsPricedInDollars()
    {
        var text = PendingQuoteMessage.Approved(OuncePending());

        AssertPricedIn(text, CurrenciesConstant.XAU_USD, 4315.20m, 4321.90m);
    }

    /// <summary>
    /// The first price screen a customer sees after picking a symbol — and, before the review of
    /// PR #321, the one that said «۴٬۳۱۹ تومان» for the ounce one message before the confirmation
    /// said «دلار».
    /// </summary>
    [Fact]
    public void BestPrices_ForTheOunce_IsPricedInDollars()
    {
        var text = BestPricesMessage.Build(4315.20m, 4321.90m, CurrenciesConstant.XAU_USD);

        AssertPricedIn(text, CurrenciesConstant.XAU_USD, 4315.20m, 4321.90m);
    }

    [Fact]
    public void BestPrices_ForGold_IsStillPricedInTomanPerMesghal()
    {
        // Gold is quoted per mesghal on this screen: 23,707,927 × 4.3318 per gram.
        var perMesghal = CurrenciesConstant.RoundToCurrencyPrecision(
            GoldPricePerGram * CurrenciesConstant.GramsPerMesghal, CurrenciesConstant.Toman);

        var text = BestPricesMessage.Build(GoldPricePerGram, GoldPricePerGram, CurrenciesConstant.MAUA_IRT);

        AssertPricedIn(text, CurrenciesConstant.MAUA_IRT, perMesghal);
    }

    private static PendingQuoteDto OuncePending() => new()
    {
        Id = Guid.NewGuid(),
        Symbol = CurrenciesConstant.XAU_USD,
        BuyPrice = 4315.20m,
        SellPrice = 4321.90m,
        ProposedMid = 4318.55m,
        PreviousMid = 4290.00m,
        DeviationPercent = 0.67m,
        BandPercent = 0.5m,
        Source = "Auto",
        CreatedAt = new DateTime(2026, 9, 22, 11, 55, 0, DateTimeKind.Utc),
        ExpiresAt = new DateTime(2026, 9, 22, 12, 25, 0, DateTimeKind.Utc)
    };
}
