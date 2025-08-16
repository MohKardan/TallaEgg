using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram
{
    public static class TelegramBotClientExtensions
    {
        public static async Task<List<long>> GetAdminUserIdsAsync(this ITelegramBotClient botClient,long chatId)
        {
            var admins = await botClient.GetChatAdministrators(chatId);

            return admins
                .Where(a => !a.User.IsBot) // فیلتر کردن ربات‌ها
                .Select(a => a.User.Id)
                .ToList();
        }
    }
}
