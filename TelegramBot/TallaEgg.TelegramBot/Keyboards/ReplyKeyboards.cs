using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Keyboards
{
    public static class ReplyKeyboards
    {
        public static async Task SendContactKeyboardAsync(this ITelegramBotClient _botClient, long chatId)
        {
            var sharePhoneButton = new KeyboardButton(BotTexts.BtnSharePhone) { RequestContact = true };

            var keyboard =  new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { sharePhoneButton }
                    })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(
                chatId,
                BotTexts.MsgPhoneRequest,
            replyMarkup: keyboard);

        }

    }
}
