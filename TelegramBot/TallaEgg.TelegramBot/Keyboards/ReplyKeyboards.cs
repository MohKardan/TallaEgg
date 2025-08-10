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

        public static async Task SendMainKeyboardAsync(this ITelegramBotClient _botClient, long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton(BotTexts.BtnCash), new KeyboardButton(BotTexts.BtnFutures) },
                new KeyboardButton[] { new KeyboardButton(BotTexts.BtnAccounting), new KeyboardButton(BotTexts.BtnHelp) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, BotTexts.MsgMainMenu, replyMarkup: keyboard);

        }

        public static async Task SendCashMenuKeyboard(this ITelegramBotClient _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
           new[]
           {
                new[] { new KeyboardButton(BotTexts.MakeOrderSpot), new KeyboardButton(BotTexts.TakeOrder) },
                new[] { new KeyboardButton(BotTexts.MainMenu)},
           }
                        )
            {
                ResizeKeyboard = true,
            };


            await _botClient.SendMessage(
                chatId,
                "🎯 منوی معاملات نقدی\n" +
    "لطفاً یکی از گزینه‌های را انتخاب کنید:",
            replyMarkup: keyboard);

        }




    }
}
