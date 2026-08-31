using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Messages;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The question an admin is shown about a held quote (issue #158).
///
/// <para>
/// Found by using the running bot: the first version showed gold in grams only. An admin who had
/// typed 333,502,239 per mesghal was asked to confirm 76,989,297.52 — arithmetically correct, and
/// a number they had never seen, in a message whose whole purpose is asking them whether the
/// number is right. It also labelled the price with the asset's weight unit, so it read as
/// "76,989,297.52 grams" rather than toman per gram.
/// </para>
/// </summary>
public class PendingQuoteMessageTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private static PendingQuoteDto Held(
        string symbol = CurrenciesConstant.MAUA_IRT,
        decimal buy = 76_989_297.52m,
        decimal sell = 76_989_297.75m,
        decimal? previousMid = 33_572_558.75m,
        decimal deviation = 129.32m,
        string source = "Manual") => new()
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            BuyPrice = buy,
            SellPrice = sell,
            ProposedMid = (buy + sell) / 2m,
            PreviousMid = previousMid,
            DeviationPercent = deviation,
            BandPercent = 5m,
            Source = source,
            CreatedAt = Now,
            ExpiresAt = Now.AddMinutes(5)
        };

    /// <summary>
    /// The exact case from the live session. 76,989,297.52 per gram is 333,502,239 per mesghal, and
    /// that is the figure the admin typed — so it has to appear, or they cannot recognise their own
    /// input.
    /// </summary>
    [Fact]
    public void AGoldQuoteShowsThePriceTheAdminTypedAsWellAsTheOneStored()
    {
        var (text, _) = PendingQuoteMessage.Build(Held(), Now);

        Assert.Contains(PersianFormat.Number(333_502_239m), text);        // per mesghal, as typed
        Assert.Contains(PersianFormat.Number(76_989_297.52m), text);      // per gram, as stored
        Assert.Contains("هر مثقال", text);
        Assert.Contains("هر گرم", text);
    }

    /// <summary>
    /// The conversion has to be the exact inverse of the one applied on the way in, through the
    /// same constant. A drift here would show the admin a price the shop is not about to publish.
    /// </summary>
    [Theory]
    [InlineData(76_989_297.52, 333_502_239)]
    [InlineData(1_000_000, 4_331_800)]
    [InlineData(33_572_558.75, 145_429_609.99)]
    public void PerMesghalIsThePerGramPriceTimesTheConversionConstant(decimal perGram, decimal expectedPerMesghal)
    {
        var (text, _) = PendingQuoteMessage.Build(Held(buy: perGram, sell: perGram), Now);

        Assert.Contains(PersianFormat.Number(expectedPerMesghal), text);
        Assert.Equal(expectedPerMesghal,
            CurrenciesConstant.RoundOrderPrice(perGram * CurrenciesConstant.GramsPerMesghal));
    }

    /// <summary>
    /// The prices are toman, and the unit is what they are per. Labelling them with the asset's
    /// weight unit turned a price into a quantity — "76,989,297.52 grams".
    /// </summary>
    [Fact]
    public void PricesAreLabelledAsTomanNotAsAWeight()
    {
        var (text, _) = PendingQuoteMessage.Build(Held(), Now);

        Assert.Contains("تومان", text);
        Assert.DoesNotContain($"{PersianFormat.Number(76_989_297.52m)} گرم", text);
    }

    /// <summary>The previous mid is converted too — comparing it against a mesghal price otherwise misleads by 4.33x.</summary>
    [Fact]
    public void ThePreviousMidIsShownInBothUnitsAsWell()
    {
        var (text, _) = PendingQuoteMessage.Build(Held(previousMid: 33_572_558.75m), Now);

        Assert.Contains(PersianFormat.Number(145_429_609.99m), text);
        Assert.Contains(PersianFormat.Number(33_572_558.75m), text);
    }

    /// <summary>A symbol that has never had a quote has no mid to show, and must not print a zero.</summary>
    [Fact]
    public void WithNoPreviousQuoteADashIsShownRatherThanZero()
    {
        var (text, _) = PendingQuoteMessage.Build(Held(previousMid: null), Now);

        Assert.Contains(BotMsgs.MsgNoPreviousQuote, text);
        Assert.DoesNotContain("میانگین قبلی: هر مثقال ۰ تومان", text);
    }

    /// <summary>
    /// A coin has one unit, not two: the price an admin types for it is already per coin. Showing a
    /// mesghal figure there would invent a unit the asset does not have.
    /// </summary>
    [Fact]
    public void ACoinQuoteShowsOneUnitOnly()
    {
        var (text, _) = PendingQuoteMessage.Build(
            Held(symbol: CurrenciesConstant.SEKE_BAHAR_IRT, buy: 187_800_000m, sell: 188_000_000m,
                 previousMid: 150_000_000m), Now);

        Assert.DoesNotContain("مثقال", text);
        Assert.Contains(PersianFormat.Number(187_800_000m), text);
        Assert.Contains("تومان", text);
    }

    [Fact]
    public void TheDeviationAndTheBandAreBothStated()
    {
        var (text, _) = PendingQuoteMessage.Build(Held(deviation: 129.32m), Now);

        Assert.Contains(PersianFormat.ToPersianDigits("129.32"), text);
        Assert.Contains(PersianFormat.ToPersianDigits("5"), text);
    }

    /// <summary>Both buttons carry the proposal's id, so an answer cannot land on the wrong symbol.</summary>
    [Fact]
    public void BothButtonsCarryTheProposalId()
    {
        var pending = Held();
        var (_, keyboard) = PendingQuoteMessage.Build(pending, Now);

        var callbacks = keyboard.InlineKeyboard.SelectMany(row => row).Select(b => b.CallbackData).ToList();

        Assert.Contains($"{InlineCallBackData.approve_quote}:{pending.Id}", callbacks);
        Assert.Contains($"{InlineCallBackData.reject_quote}:{pending.Id}", callbacks);
    }

    /// <summary>An expired proposal reads as zero minutes rather than a negative number.</summary>
    [Fact]
    public void AnExpiredProposalDoesNotShowNegativeMinutes()
    {
        var (text, _) = PendingQuoteMessage.Build(Held(), Now.AddMinutes(30));

        Assert.Contains(PersianFormat.ToPersianDigits("0"), text);
        Assert.DoesNotContain("-", text);
    }
}
