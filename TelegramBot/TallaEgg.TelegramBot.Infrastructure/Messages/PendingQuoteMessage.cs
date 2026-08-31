using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Utilties;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Messages;

/// <summary>
/// Builds the question an admin is asked about a quote the plausibility band held back
/// (issue #158).
///
/// <para>
/// One builder for both sources on purpose. An admin who typed a price and an admin who was woken
/// by the price feed are answering the same question — "is this a real price?" — and the numbers
/// they need to answer it are the same. Wording them separately is how the two drift apart.
/// </para>
///
/// <para>
/// It mirrors <see cref="QuoteMessage"/>'s split between gold and everything else, and for the
/// same reason: the admin prices gold per mesghal and the system stores it per gram, so a message
/// that names only one of the two asks them to judge a number they never typed. The first version
/// of this did exactly that — an admin who entered 333,502,239 was shown 76,989,297.52 and asked
/// whether it was right.
/// </para>
/// </summary>
public static class PendingQuoteMessage
{
    /// <summary>The text and the two buttons for one held quote.</summary>
    public static (string Text, InlineKeyboardMarkup Keyboard) Build(PendingQuoteDto pending, DateTime utcNow)
    {
        var text = pending.Symbol == CurrenciesConstant.MAUA_IRT
            ? GoldText(pending, utcNow)
            : SimpleText(pending, utcNow);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotBtns.BtnApproveQuote, $"{InlineCallBackData.approve_quote}:{pending.Id}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotBtns.BtnRejectQuote, $"{InlineCallBackData.reject_quote}:{pending.Id}")
            }
        });

        return (text, keyboard);
    }

    /// <summary>
    /// Gold, in both units. The stored figures are per gram, so the per-mesghal ones are recovered
    /// by multiplying — the exact inverse of the division <see cref="QuoteMessage"/> applied on the
    /// way in, through the same constant, so the number shown here is the number the admin typed.
    /// </summary>
    private static string GoldText(PendingQuoteDto pending, DateTime utcNow) =>
        string.Format(
            BotMsgs.MsgAdminQuoteNeedsApprovalGold,
            SourceLabel(pending.Source),
            PersianFormat.Asset(CurrenciesConstant.Maua),
            PersianFormat.Number(PerMesghal(pending.BuyPrice)),
            PersianFormat.Number(pending.BuyPrice),
            PersianFormat.Number(PerMesghal(pending.SellPrice)),
            PersianFormat.Number(pending.SellPrice),
            pending.PreviousMid is null ? BotMsgs.MsgNoPreviousQuote : PersianFormat.Number(PerMesghal(pending.PreviousMid.Value)),
            pending.PreviousMid is null ? BotMsgs.MsgNoPreviousQuote : PersianFormat.Number(pending.PreviousMid.Value),
            Percent(pending.DeviationPercent),
            Percent(pending.BandPercent),
            PersianFormat.ToPersianDigits(MinutesLeft(pending, utcNow).ToString()));

    /// <summary>
    /// A coin or a Bitcoin: one unit, because there is no second, smaller display unit for those.
    /// The price the admin types is already per traded unit.
    /// </summary>
    private static string SimpleText(PendingQuoteDto pending, DateTime utcNow)
    {
        var baseAsset = BaseAssetOf(pending.Symbol);
        var unit = CurrenciesConstant.GetCurrencyInfo(baseAsset)?.Unit ?? baseAsset;

        return string.Format(
            BotMsgs.MsgAdminQuoteNeedsApprovalSimple,
            SourceLabel(pending.Source),
            PersianFormat.Asset(baseAsset),
            PersianFormat.Number(pending.BuyPrice),
            PersianFormat.Number(pending.SellPrice),
            pending.PreviousMid is null ? BotMsgs.MsgNoPreviousQuote : PersianFormat.Number(pending.PreviousMid.Value),
            unit,
            Percent(pending.DeviationPercent),
            Percent(pending.BandPercent),
            PersianFormat.ToPersianDigits(MinutesLeft(pending, utcNow).ToString()));
    }

    /// <summary>
    /// A per-gram price back in the unit the admin prices gold in.
    ///
    /// Rounded to the price column's precision, like every other quote figure, so the mesghal
    /// number shown matches what the admin typed rather than trailing a long tail of decimals from
    /// the round trip through grams.
    /// </summary>
    private static decimal PerMesghal(decimal perGram) =>
        CurrenciesConstant.RoundOrderPrice(perGram * CurrenciesConstant.GramsPerMesghal);

    private static string Percent(decimal value) =>
        PersianFormat.ToPersianDigits(decimal.Round(value, 2).ToString());

    /// <summary>
    /// Whole minutes left before the proposal is too old to publish, never negative.
    ///
    /// Rounded up so the message never says "0 minutes" while the button still works — an admin
    /// reading that would reasonably not bother pressing it.
    /// </summary>
    private static int MinutesLeft(PendingQuoteDto pending, DateTime utcNow)
    {
        var remaining = pending.ExpiresAt - utcNow;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalMinutes);
    }

    private static string SourceLabel(string source) =>
        string.Equals(source, "Manual", StringComparison.OrdinalIgnoreCase)
            ? BotMsgs.MsgQuoteSourceManual
            : BotMsgs.MsgQuoteSourceAuto;

    /// <summary>
    /// The base asset of a BASE/QUOTE symbol, which is what the price is denominated per. Parsed
    /// defensively rather than by index, the same rule settlement follows.
    /// </summary>
    private static string BaseAssetOf(string symbol)
    {
        var parts = symbol?.Split('/');
        return parts is { Length: 2 } && !string.IsNullOrWhiteSpace(parts[0])
            ? parts[0].Trim().ToUpperInvariant()
            : CurrenciesConstant.Maua;
    }
}
