using System.Text;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

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
                return Utils.EscapeMarkdown("هیچ معامله‌ای یافت نشد.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📊 *معاملات شما – صفحه {currentPage} از {page.TotalPages}*\n");

            foreach (var t in page.Items)
            {
                sb.AppendLine(
     $"📌 *معامله #{Utils.EscapeMarkdown(t.Id.ToString()[..8])}\\…*\n" +
     $"🏷️ نماد: *{Utils.EscapeMarkdown(t.Symbol)}*\n" +
     $"💰 قیمت: *{Utils.EscapeMarkdown(t.Price.ToString("N0"))} تومان*\n" +
     $"📊 مقدار: *{Utils.EscapeMarkdown(t.Quantity.ToString("N2"))}*\n" +
     $"💵 ارزش کل: *{Utils.EscapeMarkdown(t.QuoteQuantity.ToString("N0"))} تومان*\n" +
     $"⏰ زمان: *{Utils.EscapeMarkdown(t.CreatedAt.ToString("yyyy/MM/dd HH:mm"))}*\n" +
     $"💸 کارمزد: *{Utils.EscapeMarkdown((t.FeeBuyer + t.FeeSeller).ToString("N0"))} تومان*\n");
            }

            return sb.ToString();
        }
    }
}

