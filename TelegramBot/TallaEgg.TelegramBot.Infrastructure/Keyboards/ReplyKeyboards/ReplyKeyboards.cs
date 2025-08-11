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


            await _botClient.SendMessage(
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


            await _botClient.SendMessage(
                chatId,
                "🎯 منوی اصلی\n" +
    "لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
            replyMarkup: keyboard);

        }

        public static async Task SpotMenuKeyboard(this ITelegramBotClient _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
           new[]
           {
                new[] { new KeyboardButton(ButtonTextsConstants.MakeOrder), new KeyboardButton(ButtonTextsConstants.TakeOrder) },
                new[] { new KeyboardButton(ButtonTextsConstants.MainMenu)},
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

        public static async Task SendContactKeyboardAsync(this ITelegramBotClient _botClient, long chatId)
        {
            var sharePhoneButton = new KeyboardButton(BotTexts.BtnSharePhone) { RequestContact = true };

            var keyboard = new ReplyKeyboardMarkup(new[]
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
                new KeyboardButton[] { new KeyboardButton(BotTexts.BtnSpot), new KeyboardButton(BotTexts.BtnFutures) },
                new KeyboardButton[] { new KeyboardButton(BotTexts.BtnAccounting), new KeyboardButton(BotTexts.BtnHelp) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, BotTexts.MsgMainMenu, replyMarkup: keyboard);

        }

        public static async Task SendSpotMenuKeyboard_0(this ITelegramBotClient _botClient, long chatId)
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

        public static async Task SendSpotMenuKeyboard(this ITelegramBotClient _botClient, long chatId)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("🛒 خرید نقدی", "buy_spot"),
                    InlineKeyboardButton.WithCallbackData("🛍️ فروش نقدی", "sell_spot")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendMessage(chatId, "📈 معاملات نقدی\n\nلطفاً نوع معامله خود را انتخاب کنید:", replyMarkup: keyboard);
        }
    }

}
