using System.Text;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils = TallaEgg.TelegramBot.Core.Utilties.Utils;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers
{
    public static class OrderListHandler
    {
        public static InlineKeyboardMarkup? BuildPagingKeyboard(PagedResult<OrderHistoryDto> page, int currentPage, Guid userId)
        {
            var navButtons = new List<InlineKeyboardButton>();
            if (currentPage > 1)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ قبلی", $"orders_{userId}_{currentPage - 1}"));
            if (currentPage < page.TotalPages)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("بعدی ➡️", $"orders_{userId}_{currentPage + 1}"));

            return navButtons.Any() ? new InlineKeyboardMarkup(navButtons) : null;
        }

        public static async Task<string> BuildOrdersListAsync(PagedResult<OrderHistoryDto> page, int currentPage)
        {
            if (page == null || !page.Items.Any())
            {
                return "هیچ سفارشی یافت نشد.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📋 *سفارشات شما – صفحه {currentPage} از {page.TotalPages}*\n");

            foreach (var o in page.Items)
            {
                sb.AppendLine(
     $"📌 *سفارش #{Utils.EscapeMarkdown(o.Id.ToString()[..8])}\\…*\n" +
     $"🏷️ دارایی: *{Utils.EscapeMarkdown(o.Asset)}*\n" +
     $"🔺 نوع: *{Utils.EscapeMarkdown(GetTypeIcon(o.Type))} {Utils.EscapeMarkdown(TallaEgg.Core.Utilties.Utils.GetEnumDescription(o.Type))}*\n" +
     $"📊 حجم: *{o.Amount}* | باقی‌مانده: *{o.RemainingAmount}*\n" +
     $"💰 قیمت: *{o.Price:#,0} تومان*\n" +
     $"💵 ارزش کل: *{(o.Amount * o.Price):#,0} تومان*\n" +
     $"⚡ وضعیت: *{Utils.EscapeMarkdown(GetStatusEmoji(o.Status))} {Utils.EscapeMarkdown(TallaEgg.Core.Utilties.Utils.GetEnumDescription(o.Status))}*\n" +
     $"🕓 زمان: *{Utils.EscapeMarkdown(TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(o.CreatedAt))}*" +
     (!string.IsNullOrWhiteSpace(o.Notes) ? $"\n📝 یادداشت: _{Utils.EscapeMarkdown(o.Notes)}_" : "") +
     "\n➖➖➖➖➖➖➖➖➖\n"
 );
            }
            return Utils.EscapeMarkdown(sb.ToString());
        }

        private static string GetTypeIcon(OrderSide type) => type switch
        {
            OrderSide.Buy => "🟢",
            OrderSide.Sell => "🔴",
            _ => "⚪"
        };

        private static string GetStatusEmoji(OrderStatus status) => status switch
        {
            OrderStatus.Pending => "⏳",
            OrderStatus.Confirmed => "✅",
            OrderStatus.Partially => "🔄",
            OrderStatus.Completed => "✅",
            OrderStatus.Cancelled => "❌",
            OrderStatus.Failed => "⚠️",
            _ => "❓"
        };

    }
}
