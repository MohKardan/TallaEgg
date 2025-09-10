using System.Text;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot.Types.ReplyMarkups;
using Utils = TallaEgg.TelegramBot.Core.Utilties.Utils;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers
{
    public static class ActiveOrdersHandler
    {
        public static async Task<string> BuildActiveOrdersListAsync(List<OrderHistoryDto> orders, bool isAdmin = false)
        {
            if (orders == null || !orders.Any())
            {
                return "هیچ سفارش فعالی یافت نشد.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"⚡ *سفارشات فعال* {(isAdmin ? "(همه کاربران)" : "")}\n");

            foreach (var o in orders)
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

        public static InlineKeyboardMarkup? BuildCancelOrderKeyboard(List<OrderHistoryDto> orders, bool isAdmin = false)
        {
            if (isAdmin || orders == null || !orders.Any())
                return null;

            var buttons = new List<InlineKeyboardButton>();
            
            foreach (var order in orders)
            {
                buttons.Add(InlineKeyboardButton.WithCallbackData(
                    $"❌ لغو #{order.Id.ToString()[..8]}...", 
                    $"cancel_order_{order.Id}"));
            }

            return buttons.Any() ? new InlineKeyboardMarkup(buttons) : null;
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

