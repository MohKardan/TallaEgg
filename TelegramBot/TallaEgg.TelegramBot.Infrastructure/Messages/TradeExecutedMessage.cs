using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Utilties;

namespace TallaEgg.TelegramBot.Infrastructure.Messages;

/// <summary>
/// Builds the message reporting a trade that has actually executed.
///
/// Extracted from <c>BotHandler.SendTradeExecutedAsync</c>, where the arithmetic and the
/// send were a single private async method and so could not be exercised without a live
/// Telegram connection (issue #65).
///
/// The formatting logic in this file's original home produced four defects that all
/// compiled cleanly and looked plausible in Telegram: a Persian date wrong by up to
/// 22 days, trade history that did not say whether a trade was a buy or a sell, times
/// shown in UTC, and log statements with unfilled placeholders. Each was found by a
/// person reading a message. A pure builder can be asserted on instead.
/// </summary>
public static class TradeExecutedMessage
{
    /// <param name="symbol">Trading pair, e.g. <c>MAUA/IRT</c>.</param>
    /// <param name="side">The side from the customer's point of view.</param>
    /// <param name="quantity">Filled quantity, in the base asset's own unit.</param>
    /// <param name="pricePerGram">
    /// The execution price in the unit the system stores prices in. For gold this is per
    /// gram and is converted to per mesghal for display, because the customer only ever
    /// quotes and reads gold prices per mesghal. Showing the stored number here would
    /// understate the price by a factor of 4.3318.
    /// </param>
    public static string Build(string symbol, OrderSide side, decimal quantity, decimal pricePerGram)
    {
        var baseAsset = symbol.Split('/')[0];
        var isGold = symbol == CurrenciesConstant.MAUA_IRT;
        var isBuy = side == OrderSide.Buy;

        var displayPrice = isGold
            ? pricePerGram * CurrenciesConstant.GramsPerMesghal
            : pricePerGram;

        // The total is computed from the per-gram price, never from the per-mesghal
        // display price: the displayed price is a presentation detail, and multiplying by
        // it would overstate the total by the same 4.3318.
        var total = CurrenciesConstant.RoundToCurrencyPrecision(
            quantity * pricePerGram, CurrenciesConstant.Toman);

        // Two independent signals for one fact: the coloured word here, and the direction of
        // the money below. Colour alone is not enough for someone who does not distinguish
        // the emoji or who is skimming. Same pair of labels as the trade history, so the
        // customer learns one vocabulary rather than two.
        var sideLabel = isBuy ? "🟢 خرید" : "🔴 فروش";

        // "پرداختی" (paid) vs "دریافتی" (received): the same amount means opposite things
        // to the two sides of a trade, and a single neutral label leaves the customer
        // unable to tell which happened.
        return string.Format(BotMsgs.MsgTradeExecuted,
            sideLabel,
            PersianFormat.Symbol(symbol),
            $"{PersianFormat.Amount(quantity, baseAsset)} {PersianFormat.Unit(baseAsset)}",
            isGold ? "قیمت هر مثقال" : "قیمت هر واحد",
            PersianFormat.Number(displayPrice),
            isBuy ? "پرداختی" : "دریافتی",
            PersianFormat.Number(total));
    }
}
