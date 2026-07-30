using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Utilties;

namespace TallaEgg.TelegramBot.Infrastructure.Messages;

/// <summary>
/// Builds the "confirm this order" message shown before the customer commits.
///
/// Extracted from the two places in <c>BotHandler</c> that built it inline: the dealer
/// path, where the price comes from the admin's published quote, and the limit path,
/// where the customer typed the price (issue #65).
///
/// Those two call sites used the same templates but assembled the arguments separately.
/// That is the shape of defect this class exists to prevent: the gold and non-gold
/// templates take a *different number* of arguments (6 and 5), and
/// <see cref="string.Format(string, object[])"/> with a missing argument does not fail to
/// compile — it throws at run time, in front of the customer, or silently leaves a
/// literal <c>{4}</c> in the text if the count happens to line up wrongly.
/// </summary>
public static class OrderConfirmationMessage
{
    /// <param name="symbol">Trading pair, e.g. <c>MAUA/IRT</c>.</param>
    /// <param name="side">The side from the customer's point of view.</param>
    /// <param name="quantity">Requested quantity, in the base asset's own unit.</param>
    /// <param name="pricePerGram">The price as the system stores it, and as the order will be placed.</param>
    /// <param name="displayPricePerMesghal">
    /// What to show on the "per mesghal" line for gold. Defaults to the value derived from
    /// <paramref name="pricePerGram"/>.
    ///
    /// The limit path passes the number the customer actually typed. Deriving it instead
    /// would show them a figure a few rials away from their own input — because the stored
    /// price is their input divided by <see cref="CurrenciesConstant.GramsPerMesghal"/> and
    /// the division does not round-trip exactly — and they would reasonably read that as
    /// the bot having mis-recorded their order. Ignored for non-gold pairs.
    /// </param>
    public static string Build(
        string symbol,
        OrderSide side,
        decimal quantity,
        decimal pricePerGram,
        decimal? displayPricePerMesghal = null)
    {
        var isGold = symbol == CurrenciesConstant.MAUA_IRT;
        var baseAsset = symbol.Split('/')[0];

        var amountText = $"{PersianFormat.Amount(quantity, baseAsset)} {PersianFormat.Unit(baseAsset)}";

        // Same coloured label as the executed-trade message and the trade history. The
        // customer is about to commit money, so the direction should be the most legible
        // thing on the screen, not a word buried after "نوع سفارش:".
        var sideText = side == OrderSide.Buy ? "🟢 خرید" : "🔴 فروش";

        var total = CurrenciesConstant.RoundToCurrencyPrecision(
            quantity * pricePerGram, CurrenciesConstant.Toman);

        if (!isGold)
        {
            return string.Format(BotMsgs.MsgOrderConfirmation,
                PersianFormat.Symbol(symbol),
                sideText,
                amountText,
                PersianFormat.Number(pricePerGram),
                PersianFormat.Number(total));
        }

        var pricePerMesghal = displayPricePerMesghal
                              ?? pricePerGram * CurrenciesConstant.GramsPerMesghal;

        // Gold shows both units. Showing only one is what made an earlier version
        // ambiguous: the same number can be read as a per-gram or a per-mesghal price, and
        // the two differ by 4.3318x — large enough to look like a different market, small
        // enough not to look like an obvious bug.
        return string.Format(BotMsgs.MsgOrderConfirmationGold,
            PersianFormat.Symbol(symbol),
            sideText,
            amountText,
            PersianFormat.Number(pricePerMesghal),
            PersianFormat.Number(pricePerGram),
            PersianFormat.Number(total));
    }
}
