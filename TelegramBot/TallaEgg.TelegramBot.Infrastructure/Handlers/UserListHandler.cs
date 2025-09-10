using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot.Types.ReplyMarkups;

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
                sb.AppendLine($"👤 {Utils.EscapeMarkdown(u.FirstName)} {Utils.EscapeMarkdown(u.LastName)}");
                if (!string.IsNullOrWhiteSpace(u.Username))
                    sb.AppendLine($"🔗 یوزرنیم: @{Utils.EscapeMarkdown(u.Username)}");
                if (!string.IsNullOrWhiteSpace(u.PhoneNumber))
                    sb.AppendLine($"📞 {Utils.EscapeMarkdown(u.PhoneNumber)}");
                else
                    sb.AppendLine("📞 —");

                sb.AppendLine($"📅 ثبت‌نام: {u.CreatedAt:yyyy/MM/dd HH:mm}");
                if (u.LastActiveAt.HasValue)
                    sb.AppendLine($"🕓 آخرین فعالیت: {u.LastActiveAt:yyyy/MM/dd HH:mm}");
                sb.AppendLine($"⚡ وضعیت: {Utils.EscapeMarkdown(u.Status.ToString())}");

                if (!string.IsNullOrWhiteSpace(u.PhoneNumber))
                {
                    sb.AppendLine("🔹 دستورات:");
                    sb.AppendLine($"   ▫️ موجودی → `م {Utils.EscapeMarkdown(u.PhoneNumber)}`");
                    sb.AppendLine($"   ▫️ سفارشات → `س {Utils.EscapeMarkdown(u.PhoneNumber)}`");
                }

                sb.AppendLine("──────────────────────");
            }

            return sb.ToString();
        }

    }
}
