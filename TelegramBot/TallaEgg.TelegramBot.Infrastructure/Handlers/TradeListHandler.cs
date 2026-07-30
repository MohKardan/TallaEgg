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
        /// <param name="counterpartyPhones">
        /// Phone number per counterparty user id, for the rows where it should be shown.
        ///
        /// Only populated for the admin. The admin is one side of every trade, so without the
        /// other party named their history is a list of identical-looking rows and they cannot
        /// tell which customer any of them was with. A customer needs no such row: every trade
        /// they have is with the shop, so naming it adds nothing and would expose a number
        /// they have no reason to hold.
        ///
        /// Passed in rather than looked up here so this stays a pure builder that a test can
        /// call without a users service (issue #65).
        /// </param>
        public static async Task<string> BuildTradesListAsync(
            PagedResult<TradeHistoryDto> page, int currentPage, Guid viewerUserId,
            IReadOnlyDictionary<Guid, string>? counterpartyPhones = null)
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

                // Who the trade was with. The counterparty is whichever side the viewer is
                // not — derived from isBuyer rather than assumed, so it stays correct when
                // the viewer is on either side.
                var counterpartyId = isBuyer ? t.SellerUserId : t.BuyerUserId;
                if (counterpartyPhones is not null &&
                    counterpartyPhones.TryGetValue(counterpartyId, out var phone) &&
                    !string.IsNullOrWhiteSpace(phone))
                {
                    sb.AppendLine($"👤 طرف معامله: {PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone))}");
                }

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

