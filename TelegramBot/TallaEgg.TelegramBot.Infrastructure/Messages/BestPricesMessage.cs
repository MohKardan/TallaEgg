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
    public static string Build(decimal? bestBidPrice, decimal? bestAskPrice) =>
        string.Format(BotMsgs.MsgBestPrices, Format(bestBidPrice), Format(bestAskPrice));

    private static string Format(decimal? price) => price.HasValue
        ? $"{PersianFormat.Number(price.Value)} تومان"
        : BotMsgs.MsgPriceNotAvailable;
}
