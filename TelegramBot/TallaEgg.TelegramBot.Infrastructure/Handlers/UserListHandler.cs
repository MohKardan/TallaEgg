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

        public static Task<string> BuildUsersListAsync(PagedResult<UserDto> page, int currentPage, string? query)
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
                    // «معامله» shows the customer's completed trades. It was «س» — سفارش, orders —
                    // back when customers placed orders against each other and an open-order list
                    // was a real thing to inspect. The dealer model left that command showing
                    // trades, so the letter stopped matching the work, and «س» is now reserved
                    // against order placement returning (docs/product/DIRECTION.md).
                    //
                    // A third line offered «سفارشات باز» as «ف» and is gone (issue #293). No dispatch
                    // for it had ever existed — git log -S 'StartsWith("ف' finds nothing in the whole
                    // history — so from the day it was added, 2025-09-10, an admin who typed it
                    // silently got the main menu.
                    //
                    // The reason first written here for removing it, that the dealer model keeps
                    // such a list empty, was wrong in a way worth remembering: it is only true
                    // while the dealer model is the whole product. The line went because it
                    // advertised a command nobody had implemented, which is true either way.
                    sb.AppendLine("🔹 دستورات:");
                    sb.AppendLine($"   ▫️ موجودی → `م {Utils.EscapeMarkdownV2(u.PhoneNumber)}`");
                    sb.AppendLine($"   ▫️ معامله‌ها → `معامله {Utils.EscapeMarkdownV2(u.PhoneNumber)}`");
                }

                sb.AppendLine("──────────────────────");
            }

            return Task.FromResult(sb.ToString());
        }

    }
}
