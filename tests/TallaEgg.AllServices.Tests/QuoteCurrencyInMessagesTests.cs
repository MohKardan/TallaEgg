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

    [Fact]
    public void OrderConfirmation_ForTheOunce_IsPricedInDollars()
    {
        var text = OrderConfirmationMessage.Build(CurrenciesConstant.XAU_USD, OrderSide.Buy, 0.5m, OuncePrice);

        Assert.Contains(Dollar, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Toman, text, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderConfirmation_ForGold_IsStillPricedInToman()
    {
        var text = OrderConfirmationMessage.Build(CurrenciesConstant.MAUA_IRT, OrderSide.Buy, 8m, GoldPricePerGram);

        Assert.Contains(Toman, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Dollar, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The total is rounded to the quote asset's precision, which is cents for dollars and whole
    /// units for toman. Rounding a dollar total the toman way would drop up to 99 cents from the
    /// figure the customer is asked to approve.
    /// </summary>
    [Fact]
    public void OrderConfirmation_ForTheOunce_KeepsTheCentsInTheTotal()
    {
        // 0.5 × 4318.55 = 2159.275 → 2159.28 at the dollar's two decimals, and the price itself
        // keeps its cents rather than being shown as a whole number of dollars.
        var text = OrderConfirmationMessage.Build(CurrenciesConstant.XAU_USD, OrderSide.Buy, 0.5m, OuncePrice);

        Assert.Contains(PersianFormat.Amount(2159.28m, CurrenciesConstant.Usd), text, StringComparison.Ordinal);
        Assert.Contains(PersianFormat.Amount(OuncePrice, CurrenciesConstant.Usd), text, StringComparison.Ordinal);
    }

    [Fact]
    public void TradeExecuted_ForTheOunce_IsPricedInDollars()
    {
        var text = TradeExecutedMessage.Build(CurrenciesConstant.XAU_USD, OrderSide.Sell, 1.25m, OuncePrice);

        Assert.Contains(Dollar, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Toman, text, StringComparison.Ordinal);
    }

    [Fact]
    public void TradeExecuted_ForTheCoin_IsStillPricedInToman()
    {
        var text = TradeExecutedMessage.Build(CurrenciesConstant.SEKE_BAHAR_IRT, OrderSide.Sell, 2m, 230_130_000m);

        Assert.Contains(Toman, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Dollar, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The admin's own confirmation after publishing. They are being shown the prices customers
    /// will trade at, so a wrong currency here is wrong before a single customer sees it.
    /// </summary>
    [Fact]
    public void PublishedQuote_ForTheOunce_IsPricedInDollars()
    {
        var text = QuoteMessage.Prepare(CurrenciesConstant.XAU_USD, 4315.20m, 4321.90m).Text;

        Assert.Contains(Dollar, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Toman, text, StringComparison.Ordinal);
    }

    [Fact]
    public void HeldQuote_ForTheOunce_IsPricedInDollars()
    {
        var (text, _) = PendingQuoteMessage.Build(OuncePending(), new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains(Dollar, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Toman, text, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedQuote_ForTheOunce_IsPricedInDollars()
    {
        var text = PendingQuoteMessage.Approved(OuncePending());

        Assert.Contains(Dollar, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Toman, text, StringComparison.Ordinal);
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
