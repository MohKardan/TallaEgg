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

        public static async Task<string> BuildTradesListAsync(PagedResult<TradeHistoryDto> page, int currentPage)
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

                sb.AppendLine($"📌 معاملهٔ {PersianFormat.Ltr(t.Id.ToString()[..8])}");
                sb.AppendLine($"🏷️ دارایی: {PersianFormat.Symbol(t.Symbol)}");
                sb.AppendLine($"📊 مقدار: {PersianFormat.Amount(t.Quantity, baseAsset)} {unit}");
                sb.AppendLine($"💰 {priceLabel}: {PersianFormat.Number(displayPrice)} تومان");
                sb.AppendLine($"💵 ارزش کل: {PersianFormat.Number(t.QuoteQuantity)} تومان");
                sb.AppendLine($"🕓 زمان: {PersianFormat.ToPersianDigits(TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(t.CreatedAt))}");
                sb.AppendLine("➖➖➖➖➖➖➖➖➖");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}

