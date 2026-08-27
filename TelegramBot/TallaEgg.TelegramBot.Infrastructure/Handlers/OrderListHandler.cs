using TallaEgg.Core;
﻿using System.Text;
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

            // Plain text, no Markdown — the same reason as ActiveOrdersHandler: EscapeMarkdownV2
            // neutralised the formatting markers and they were displayed raw.
            var sb = new StringBuilder();
            sb.AppendLine($"📋 سفارش‌های شما — صفحهٔ {PersianFormat.Number(currentPage)} از {PersianFormat.Number(page.TotalPages)}");
            sb.AppendLine();

            foreach (var o in page.Items)
            {
                var baseAsset = o.Asset.Split('/')[0];
                var unit = PersianFormat.Unit(baseAsset);
                var isGold = o.Asset == CurrenciesConstant.MAUA_IRT;
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
