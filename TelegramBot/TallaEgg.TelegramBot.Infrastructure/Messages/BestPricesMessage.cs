using TallaEgg.Core;
using TallaEgg.Core.Utilties;

namespace TallaEgg.TelegramBot.Infrastructure.Messages;

/// <summary>
/// Builds the "best market prices" message (issue #65).
///
/// Extracted for the absent-price case. When one side of the book is empty the price is
/// null, and rendering it as a number gives "۰ تومان" — which reads as a price of zero, not
/// as the absence of a price. A customer seeing gold bid at zero would reasonably conclude
/// the market had collapsed.
/// </summary>
public static class BestPricesMessage
{
    /// <summary>
    /// Gold is quoted internally per gram but shown to customers per mesghal — the same
    /// conversion every other trade/order/quote message in the bot already applies (see
    /// <c>OrderConfirmationMessage</c>, <c>TradeExecutedMessage</c>, <c>QuoteHistoryHandler</c>).
    /// Every other symbol has no such internal/display split, so its price is shown as-is
    /// under its own base unit (e.g. «سکه», «بیت‌کوین») instead of the gold-specific «مثقال».
    /// </summary>
    public static string Build(decimal? bestBidPrice, decimal? bestAskPrice, string symbol)
    {
        var isGold = symbol == CurrenciesConstant.MAUA_IRT;
        var unit = isGold ? "مثقال" : CurrenciesConstant.GetTradingPairInfo(symbol)?.BaseUnit ?? "واحد";

        return string.Format(BotMsgs.MsgBestPrices, unit, Format(bestBidPrice, isGold), Format(bestAskPrice, isGold));
    }

    private static string Format(decimal? price, bool isGold)
    {
        if (!price.HasValue)
            return BotMsgs.MsgPriceNotAvailable;

        var displayPrice = isGold ? price.Value * CurrenciesConstant.GramsPerMesghal : price.Value;
        return $"{PersianFormat.Number(displayPrice)} تومان";
    }
}
