using TallaEgg.Core;
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

        /// <summary>
        /// فهرست معاملات کاربر.
        /// </summary>
        /// <param name="viewerUserId">
        /// کاربری که فهرست را می‌بیند. لازم است چون «خرید یا فروش» یک ویژگی خودِ معامله
        /// نیست، بلکه به این بستگی دارد که بیننده کدام طرف معامله بوده: یک معاملهٔ واحد
        /// برای یک طرف خرید است و برای طرف دیگر فروش.
        /// </param>
        public static async Task<string> BuildTradesListAsync(
            PagedResult<TradeHistoryDto> page, int currentPage, Guid viewerUserId)
        {
            if (page == null || !page.Items.Any())
            {
                return "هیچ معامله‌ای انجام نشده است.";
            }

            // متن ساده: نسخهٔ قبلی برچسب‌های HTML و نشانه‌های Markdown را با هم مخلوط
            // کرده بود که هیچ‌کدام درست نمایش داده نمی‌شد.
            var sb = new StringBuilder();
            sb.AppendLine($"📊 معاملات شما — صفحهٔ {PersianFormat.Number(currentPage)} از {PersianFormat.Number(page.TotalPages)}");
            sb.AppendLine();

            foreach (var t in page.Items)
            {
                var baseAsset = t.Symbol.Split('/')[0];
                var unit = PersianFormat.Unit(baseAsset);
                var isGold = t.Symbol == CurrenciesConstant.MAUA_IRT;
                var displayPrice = isGold ? t.Price * CurrenciesConstant.GramsPerMesghal : t.Price;
                var priceLabel = isGold ? "قیمت هر مثقال" : "قیمت هر واحد";

                var isBuyer = t.BuyerUserId == viewerUserId;

                // دو نشانهٔ مستقل برای یک واقعیت: برچسب صریح در بالا، و جهت پول در پایین.
                // فقط رنگ ایموجی کافی نیست — کاربری که رنگ‌ها را تفکیک نمی‌کند یا پیام را
                // سریع مرور می‌کند باید از روی خود کلمه‌ها بفهمد.
                var sideLabel = isBuyer ? "🟢 خرید" : "🔴 فروش";

                // «ارزش کل» نمی‌گفت این پول از حساب کاربر رفته یا به آن آمده. برای خریدار
                // این مبلغ پرداختی است و برای فروشنده دریافتی.
                var amountLabel = isBuyer ? "پرداختی" : "دریافتی";

                sb.AppendLine($"📌 معاملهٔ {PersianFormat.Ltr(t.Id.ToString()[..8])}");
                sb.AppendLine(sideLabel);
                sb.AppendLine($"🏷️ دارایی: {PersianFormat.Symbol(t.Symbol)}");
                sb.AppendLine($"📊 مقدار: {PersianFormat.Amount(t.Quantity, baseAsset)} {unit}");
                sb.AppendLine($"💰 {priceLabel}: {PersianFormat.Number(displayPrice)} تومان");
                sb.AppendLine($"💵 {amountLabel}: {PersianFormat.Number(t.QuoteQuantity)} تومان");
                sb.AppendLine($"🕓 زمان: {PersianFormat.ToPersianDigits(TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(t.CreatedAt))}");
                sb.AppendLine("➖➖➖➖➖➖➖➖➖");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}

