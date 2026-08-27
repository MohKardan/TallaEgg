using TallaEgg.Core;
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
        /// <summary>
        /// The active order list, as plain text.
        ///
        /// Markdown is deliberately not used: the previous message was sent as MarkdownV2, but
        /// EscapeMarkdownV2 escaped the bold markers themselves, so the asterisks reached the user as
        /// raw text. Plain text renders correctly and is immune to escaping hazards.
        ///
        /// Prices are shown per mesghal, because that is the unit users and admins enter them in.
        /// Storage is per gram.
        /// </summary>
        public static async Task<string> BuildActiveOrdersListAsync(List<OrderHistoryDto> orders, bool isAdmin = false)
        {
            if (orders == null || !orders.Any())
            {
                return "هیچ سفارش فعالی وجود ندارد.";
            }

            var sb = new StringBuilder();
            sb.AppendLine(isAdmin ? "⚡ سفارش‌های فعال (همهٔ کاربران)" : "⚡ سفارش‌های فعال شما");
            sb.AppendLine();

            foreach (var o in orders)
            {
                var baseAsset = o.Asset.Split('/')[0];
                var unit = PersianFormat.Unit(baseAsset);
                var isGold = o.Asset == CurrenciesConstant.MAUA_IRT;

                // قیمت ذخیره‌شده بر حسب گرم است؛ برای طلا به مثقال تبدیل می‌شود.
                var displayPrice = isGold ? o.Price * CurrenciesConstant.GramsPerMesghal : o.Price;
                var priceLabel = isGold ? "قیمت هر مثقال" : "قیمت هر واحد";

                sb.AppendLine($"📌 سفارش {PersianFormat.Ltr(o.Id.ToString()[..8])}");
                sb.AppendLine($"🏷️ دارایی: {PersianFormat.Symbol(o.Asset)}");
                sb.AppendLine($"{GetTypeIcon(o.Type)} نوع: {TallaEgg.Core.Utilties.Utils.GetEnumDescription(o.Type)}");
                sb.AppendLine($"📊 مقدار: {PersianFormat.Amount(o.Amount, baseAsset)} {unit}");
                sb.AppendLine($"⏳ باقی‌مانده: {PersianFormat.Amount(o.RemainingAmount, baseAsset)} {unit}");
                sb.AppendLine($"💰 {priceLabel}: {PersianFormat.Number(displayPrice)} تومان");
                sb.AppendLine($"💵 ارزش کل: {PersianFormat.Number(o.Amount * o.Price)} تومان");
                sb.AppendLine($"{GetStatusEmoji(o.Status)} وضعیت: {TallaEgg.Core.Utilties.Utils.GetEnumDescription(o.Status)}");
                sb.AppendLine($"🕓 زمان: {PersianFormat.ToPersianDigits(TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(o.CreatedAt))}");

                if (!string.IsNullOrWhiteSpace(o.Notes))
                    sb.AppendLine($"📝 یادداشت: {o.Notes}");

                sb.AppendLine("➖➖➖➖➖➖➖➖➖");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static InlineKeyboardMarkup? BuildCancelOrderKeyboard(List<OrderHistoryDto> orders, bool isAdmin = false)
        {
            if (isAdmin || orders == null || !orders.Any())
                return null;

            var buttons = new List<InlineKeyboardButton>();
            
            foreach (var order in orders)
            {
                buttons.Add(InlineKeyboardButton.WithCallbackData(
                    $"❌ لغو سفارش {order.Id.ToString()[..8]}",
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

