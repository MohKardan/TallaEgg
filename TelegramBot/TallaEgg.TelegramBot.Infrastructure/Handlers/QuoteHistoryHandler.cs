using System.Text;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Utilties;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers;

/// <summary>
/// The list of prices the shop has published, newest first.
///
/// This replaced order history in the accounting menu. In the dealer model an order exists
/// only for the instant of a fill — it is created, matched and consumed in one operation —
/// so a customer's order list was always either empty or made entirely of completed rows.
/// It showed activity without telling anyone anything they could act on.
///
/// The published prices are the history that does mean something: what the shop was
/// offering, when it changed, and what the margin was at the time.
/// </summary>
public static class QuoteHistoryHandler
{
    /// <summary>Callback prefix for paging. Kept short — Telegram caps callback data at 64 bytes.</summary>
    public const string CallbackPrefix = "quotes_";

    public static InlineKeyboardMarkup? BuildPagingKeyboard(PagedResult<QuoteDto> page, int currentPage, string symbol)
    {
        var navButtons = new List<InlineKeyboardButton>();

        if (currentPage > 1)
            navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ قبلی", $"{CallbackPrefix}{symbol}_{currentPage - 1}"));

        if (currentPage < page.TotalPages)
            navButtons.Add(InlineKeyboardButton.WithCallbackData("بعدی ➡️", $"{CallbackPrefix}{symbol}_{currentPage + 1}"));

        return navButtons.Count > 0 ? new InlineKeyboardMarkup(navButtons) : null;
    }

    /// <param name="isAdmin">
    /// Admins set these prices, so for them the two sides are "you buy" and "you sell". A
    /// customer trades against them, so the same two numbers are the prices *they* sell and
    /// buy at. One row of data, two readings — labelling it for only one audience is how the
    /// buyer/seller inversion keeps reappearing in this product.
    /// </param>
    public static string BuildQuoteHistoryAsync(PagedResult<QuoteDto> page, int currentPage, bool isAdmin)
    {
        if (page is null || !page.Items.Any())
            return "هنوز مظنه‌ای منتشر نشده است.";

        var sb = new StringBuilder();
        sb.AppendLine($"📋 تاریخچهٔ مظنه‌ها — صفحهٔ {PersianFormat.Number(currentPage)} از {PersianFormat.Number(page.TotalPages)}");
        sb.AppendLine();

        foreach (var q in page.Items)
        {
            var isGold = q.Symbol == CurrenciesConstant.MAUA_IRT;

            // Prices are stored per gram and read per mesghal, the unit both the admin and
            // the customer actually speak in.
            var buyDisplay = isGold ? q.BuyPrice * CurrenciesConstant.GramsPerMesghal : q.BuyPrice;
            var sellDisplay = isGold ? q.SellPrice * CurrenciesConstant.GramsPerMesghal : q.SellPrice;
            var unitLabel = isGold
                ? "هر مثقال"
                : $"هر {CurrenciesConstant.GetCurrencyInfo(q.Symbol.Split('/')[0])?.Unit ?? "واحد"}";

            // The active quote is the one being traded on right now; every other row is
            // history. Saying so removes the guess about whether the top row is still live.
            sb.AppendLine(q.IsActive ? "🟩 مظنهٔ فعال" : "🕗 منقضی شد");

            if (isAdmin)
            {
                sb.AppendLine($"🟢 شما می‌خرید — {unitLabel}: {PersianFormat.Number(buyDisplay)} تومان");
                sb.AppendLine($"🔴 شما می‌فروشید — {unitLabel}: {PersianFormat.Number(sellDisplay)} تومان");
                sb.AppendLine($"📈 حاشیه: {PersianFormat.Number(sellDisplay - buyDisplay)} تومان");
            }
            else
            {
                sb.AppendLine($"🔴 قیمت فروش شما — {unitLabel}: {PersianFormat.Number(buyDisplay)} تومان");
                sb.AppendLine($"🟢 قیمت خرید شما — {unitLabel}: {PersianFormat.Number(sellDisplay)} تومان");
            }

            sb.AppendLine($"🕓 انتشار: {PersianFormat.ToPersianDigits(TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(q.PublishedAt))}");

            if (q.DeactivatedAt.HasValue)
                sb.AppendLine($"🕓 پایان: {PersianFormat.ToPersianDigits(TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(q.DeactivatedAt.Value))}");

            sb.AppendLine("➖➖➖➖➖➖➖➖➖");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
