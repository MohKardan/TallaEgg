using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot.Types.ReplyMarkups;
using PersianFormat = TallaEgg.Core.Utilties.PersianFormat;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers
{
    public static class UserListHandler
    {
        public static InlineKeyboardMarkup? BuildPagingKeyboard(PagedResult<UserDto> page, int currentPage, string? query)
        {
            var navButtons = new List<InlineKeyboardButton>();
            if (currentPage > 1)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ قبلی", $"users_{currentPage - 1}_{query}"));
            if (currentPage < page.TotalPages)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("بعدی ➡️", $"users_{currentPage + 1}_{query}"));

            return navButtons.Any() ? new InlineKeyboardMarkup(navButtons) : null;
        }

        public static async Task<string> BuildUsersListAsync(PagedResult<UserDto> page, int currentPage, string? query)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"👥 لیست کاربران – صفحه {currentPage} از {page.TotalPages}\n");

            foreach (var u in page.Items)
            {
                sb.AppendLine($"👤 {Utils.EscapeMarkdownV2(u.FirstName)} {Utils.EscapeMarkdownV2(u.LastName)}");
                if (!string.IsNullOrWhiteSpace(u.Username))
                    sb.AppendLine($"🔗 یوزرنیم: @{Utils.EscapeMarkdownV2(u.Username)}");
                if (!string.IsNullOrWhiteSpace(u.PhoneNumber))
                    sb.AppendLine($"📞 {Utils.EscapeMarkdownV2(u.PhoneNumber)}");
                else
                    sb.AppendLine("📞 —");

                // Through the shared formatter, so these read as Jalali in Tehran time like
                // every other date the bot shows. They were Gregorian and in UTC.
                sb.AppendLine($"📅 ثبت‌نام: {PersianFormat.DateTimeText(u.CreatedAt)}");
                if (u.LastActiveAt.HasValue)
                    sb.AppendLine($"🕓 آخرین فعالیت: {PersianFormat.DateTimeText(u.LastActiveAt.Value)}");
                sb.AppendLine($"⚡ وضعیت: {Utils.EscapeMarkdownV2(u.Status.ToString())}");

                if (!string.IsNullOrWhiteSpace(u.PhoneNumber))
                {
                    sb.AppendLine("🔹 دستورات:");
                    sb.AppendLine($"   ▫️ موجودی → `م {Utils.EscapeMarkdownV2(u.PhoneNumber)}`");
                    sb.AppendLine($"   ▫️ سفارشات → `س {Utils.EscapeMarkdownV2(u.PhoneNumber)}`");
                    sb.AppendLine($"   ▫️ سفارشات باز → `ف {Utils.EscapeMarkdownV2(u.PhoneNumber)}`");
                }

                sb.AppendLine("──────────────────────");
            }

            return sb.ToString();
        }

    }
}
