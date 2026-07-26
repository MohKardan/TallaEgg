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
        /// فهرست سفارش‌های فعال به‌صورت متن ساده.
        ///
        /// عمداً از Markdown استفاده نمی‌شود: پیام قبلی با MarkdownV2 ارسال می‌شد اما
        /// EscapeMarkdownV2 خودِ نشانه‌های bold را هم escape می‌کرد، بنابراین ستاره‌ها
        /// به‌صورت متن خام به کاربر نمایش داده می‌شدند. متن ساده هم درست نمایش داده
        /// می‌شود و هم از خطرهای escape شدن در امان است.
        ///
        /// قیمت بر حسب «هر مثقال» نشان داده می‌شود، چون کاربر و مدیر قیمت را با همین
        /// واحد وارد می‌کنند (در دیتابیس بر حسب «هر گرم» ذخیره می‌شود).
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

