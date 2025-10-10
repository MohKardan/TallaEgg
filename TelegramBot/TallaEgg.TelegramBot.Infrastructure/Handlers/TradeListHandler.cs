using System.Text;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils = TallaEgg.TelegramBot.Core.Utilties.Utils;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers
{
    public static class TradeListHandler
    {
        public static InlineKeyboardMarkup? BuildPagingKeyboard(PagedResult<TradeHistoryDto> page, int currentPage, Guid userId)
        {
            var navButtons = new List<InlineKeyboardButton>();
            if (currentPage > 1)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ قبلی", $"trades_{userId}_{currentPage - 1}"));
            if (currentPage < page.TotalPages)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("بعدی ➡️", $"trades_{userId}_{currentPage + 1}"));

            return navButtons.Any() ? new InlineKeyboardMarkup(navButtons) : null;
        }

        public static async Task<string> BuildTradesListAsync(PagedResult<TradeHistoryDto> page, int currentPage)
        {
            if (page == null || !page.Items.Any())
            {
                return Utils.EscapeMarkdownV2("هیچ معامله‌ای یافت نشد.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📊 *معاملات شما – صفحه {currentPage} از {page.TotalPages}*\n");

            foreach (var t in page.Items)
            {
                sb.AppendLine(
    $"📌 <b>معامله #{t.Id.ToString()[..8]}…</b>\n" +
$"🏷️ نماد: <b>{t.Symbol}</b>\n" +
$"💰 قیمت: <b>{t.Price:N0} تومان</b>\n" +
$"📊 مقدار: <b>{t.Quantity:N2}</b>\n" +
$"💵 ارزش کل: <b>{t.QuoteQuantity:N0} تومان</b>\n" +
$"⏰ زمان: <b>{TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(t.CreatedAt)}</b>\n" +
$"💸 کارمزد: <b>{(t.FeeBuyer + t.FeeSeller):N0} تومان</b>\n"
     );
            }

            return sb.ToString();
        }
    }
}

