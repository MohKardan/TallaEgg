using TallaEgg.Core;
using TallaEgg.Core.Utilties;

namespace TallaEgg.TelegramBot.Infrastructure.Messages;

/// <summary>
/// Prepares an admin quote for publication: converts the prices the admin typed into the
/// prices the system stores, and builds the confirmation the admin sees.
///
/// Extracted from <c>BotHandlerAdmin.HandlePricePairOrdersAsync</c> (issue #65).
///
/// Conversion and message are deliberately produced together by
/// <see cref="Prepare"/> rather than exposed as two independent steps. The caller used to
/// compute the per-gram prices, publish them, and then separately format a message from
/// the same inputs — two calculations that were only equal by convention. Returning one
/// value carrying both makes "the price published is the price displayed" true by
/// construction, which matters here because a mismatch would mean every customer trades
/// at a price the shop owner never saw.
/// </summary>
public static class QuoteMessage
{
    /// <param name="symbol">Trading pair, e.g. <c>MAUA/IRT</c>.</param>
    /// <param name="buyPrice">
    /// The price the admin buys at — what the customer sells at. Per mesghal for MAUA/IRT (the
    /// unit an admin already prices gold in by hand); per traded unit for every other symbol
    /// (per coin, per Bitcoin — there is no second, smaller display unit for those).
    /// </param>
    /// <param name="sellPrice">The price the admin sells at — what the customer buys at. Same unit as <paramref name="buyPrice"/>.</param>
    public static QuotePublication Prepare(string symbol, decimal buyPrice, decimal sellPrice)
    {
        if (symbol == CurrenciesConstant.MAUA_IRT)
            return PrepareGold(symbol, buyPrice, sellPrice);

        // Every other symbol: what the admin typed already is the per-unit price. No
        // mesghal-to-gram conversion applies — MAUA/IRT is the only symbol with that duality.
        var roundedBuy = CurrenciesConstant.RoundOrderPrice(buyPrice);
        var roundedSell = CurrenciesConstant.RoundOrderPrice(sellPrice);
        var margin = roundedSell - roundedBuy;

        var baseAsset = symbol.Split('/')[0];
        var unit = CurrenciesConstant.GetCurrencyInfo(baseAsset)?.Unit ?? baseAsset;

        var text = string.Format(BotMsgs.MsgAdminQuotePublishedSimple,
            PersianFormat.Symbol(symbol),
            PersianFormat.Number(roundedBuy),
            PersianFormat.Number(roundedSell),
            PersianFormat.Number(margin),
            unit);

        return new QuotePublication(roundedBuy, roundedSell, text);
    }

    private static QuotePublication PrepareGold(string symbol, decimal buyPricePerMesghal, decimal sellPricePerMesghal)
    {
        // The admin types prices per mesghal; orders and quotes are stored per gram.
        // Rounded to the precision of the price column, so the value published is exactly
        // the value that will be persisted rather than one the database silently truncates.
        var buyPricePerGram = CurrenciesConstant.RoundOrderPrice(
            buyPricePerMesghal / CurrenciesConstant.GramsPerMesghal);

        var sellPricePerGram = CurrenciesConstant.RoundOrderPrice(
            sellPricePerMesghal / CurrenciesConstant.GramsPerMesghal);

        // The margin is quoted per mesghal, the unit the admin typed, so it is directly
        // comparable to the two prices above it. Deriving it from the per-gram figures
        // instead would show a number 4.3318x smaller than the units around it.
        var marginPerMesghal = sellPricePerMesghal - buyPricePerMesghal;

        var text = string.Format(BotMsgs.MsgAdminQuotePublished,
            PersianFormat.Symbol(symbol),
            PersianFormat.Number(buyPricePerMesghal),
            PersianFormat.Number(buyPricePerGram),
            PersianFormat.Number(sellPricePerMesghal),
            PersianFormat.Number(sellPricePerGram),
            PersianFormat.Number(marginPerMesghal));

        return new QuotePublication(buyPricePerGram, sellPricePerGram, text);
    }
}

/// <summary>
/// The per-gram prices to publish and the confirmation text describing them. Always
/// obtained together so the two cannot disagree.
/// </summary>
public sealed record QuotePublication(
    decimal BuyPricePerGram,
    decimal SellPricePerGram,
    string Text);
