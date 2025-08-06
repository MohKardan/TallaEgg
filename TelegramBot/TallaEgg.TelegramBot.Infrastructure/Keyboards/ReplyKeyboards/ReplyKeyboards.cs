using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Keyboards.ReplyKeyboards
{
    public static class ReplyKeyboards
    {
        public static async Task RequestContactKeyboard(this ITelegramBotClient _botClient, long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(
                 new[]
                 {
                    new[]
                    {
                        KeyboardButton.WithRequestContact("ارسال شماره همراه")
                    }
                 }
             )
            {
                ResizeKeyboard = true
            };


            await _botClient.SendTextMessageAsync(
                chatId,
                "برای ثبت نام شماره خود را از طریق کلید زیر ارسال کنید",
            replyMarkup: keyboard);

        }

        public static async Task MainMenuKeyboard(this ITelegramBotClient _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
           new[]
           {
                new[] { new KeyboardButton(ButtonTextsConstants.Spot), new KeyboardButton(ButtonTextsConstants.Future) },

                new[] { new KeyboardButton(ButtonTextsConstants.Accounting), new KeyboardButton(ButtonTextsConstants.Help) },
                new[] { new KeyboardButton(ButtonTextsConstants.Wallet), new KeyboardButton(ButtonTextsConstants.History) },
           }
       )
            {
                ResizeKeyboard = true,
            };


            await _botClient.SendTextMessageAsync(
                chatId,
                "🎯 منوی اصلی\n" +
    "لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
            replyMarkup: keyboard);

        }
    }
}
