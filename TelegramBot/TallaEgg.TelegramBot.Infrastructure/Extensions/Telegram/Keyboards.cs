using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram
{
    public static class Keyboards
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

        // MainMenuKeyboard اینجا حذف شد: از هیچ‌جا صدا زده نمی‌شد و تنها جایی بود که دکمهٔ
        // «📈 آتی» را نشان می‌داد — دکمه‌ای برای بازاری که وجود ندارد و هیچ handlerی هم
        // نداشت. منوی اصلیِ واقعی جای دیگری ساخته می‌شود.

        public static async Task SendContactKeyboardAsync(this ITelegramBotClient _botClient, long chatId)
        {
            var sharePhoneButton = new KeyboardButton(BotBtns.BtnSharePhone) { RequestContact = true };

            var keyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { sharePhoneButton }
                    })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(
                chatId,
                BotMsgs.MsgPhoneRequest,
            replyMarkup: keyboard);

        }
        /// <summary>
        /// منوی اصلی برای کاربر عادی و مدیر فرق میکنه
        /// </summary>
        /// <param name="_botClient"></param>
        /// <param name="chatId"></param>
        /// <returns></returns>
        public static async Task SendMainKeyboardForAdminAsync(this ITelegramBotClient _botClient, long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                //new KeyboardButton[] { new KeyboardButton(BotBtns.BtnSpotCreateOrder) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnSpotSubmitPrice) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnActiveOrders), new KeyboardButton(BotBtns.BtnAccounting) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnHelp) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, BotMsgs.MsgMainMenu, replyMarkup: keyboard);
        }
        /// <summary>
        /// منوی اصلی برای کاربر عادی و مدیر فرق میکنه
        /// </summary>
        /// <param name="_botClient"></param>
        /// <param name="chatId"></param>
        /// <returns></returns>
        public static async Task SendMainKeyboardForUserAsync(this ITelegramBotClient _botClient, long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnSpotMarket) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnHelp), new KeyboardButton(BotBtns.BtnAccounting) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, BotMsgs.MsgMainMenu, replyMarkup: keyboard);
        }

        public static async Task SendAccountingMenuKeyboard(this ITelegramBotClient _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
                new[]
               {
                    new[] { new KeyboardButton(BotBtns.BtnTradeHistory)},
                    new[] { new KeyboardButton(BotBtns.BtnMainMenu)},
               }
               //new[]
               //{
               //     new[] { new KeyboardButton(BotBtns.BtnOrderHistory), new KeyboardButton(BotBtns.BtnTradeHistory)},
               //     new[] { new KeyboardButton(BotBtns.BtnActiveOrders), new KeyboardButton(BotBtns.BtnWalletsBalance)},
               //     new[] { new KeyboardButton(BotBtns.BtnMainMenu)},
               //}
                            )
            {
                ResizeKeyboard = true,
            };


            await _botClient.SendMessage(
                chatId,
                "📑 منوی حسابداری\n" +
                "لطفاً یکی از گزینه‌های را انتخاب کنید:",
            replyMarkup: keyboard);

        }
        public static async Task SendAccountingMenuKeyboardForAdmin(this ITelegramBotClient _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
               new[]
               {
                    new[] { new KeyboardButton(BotBtns.BtnOrderHistory), new KeyboardButton(BotBtns.BtnTradeHistory)},
                    new[] { new KeyboardButton(BotBtns.BtnActiveOrders) },
                    new[] { new KeyboardButton(BotBtns.BtnMainMenu)},
               }
                            )
            {
                ResizeKeyboard = true,
            };


            await _botClient.SendMessage(
                chatId,
                "📑 منوی حسابداری\n" +
                "لطفاً یکی از گزینه‌های را انتخاب کنید:",
            replyMarkup: keyboard);

        }
        public static async Task SendSpotSideMenuKeyboard(this ITelegramBotClient _botClient, long chatId)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnSpotMarketBuy, InlineCallBackData.buy_spot),
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnSpotMarketSell, InlineCallBackData.sell_spot)
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, InlineCallBackData.back_to_main)
                }
            });

            await _botClient.SendMessage(chatId, "📈 معاملات نقدی\n\nلطفاً نوع معامله خود را انتخاب کنید:", replyMarkup: keyboard);
        }

        // SendUserOrdersWithPagingAsync و کمکی‌هایش (GetTypeIcon/GetStatusEmoji) حذف شدند.
        //
        // از هیچ‌جا صدا زده نمی‌شد — نسخهٔ زندهٔ فهرست سفارش‌ها در OrderListHandler است.
        // این نسخهٔ متروک تنها جایی بود که o.Role را به کاربر نشان می‌داد، مقداری که
        // همیشه Maker است و درست نیست (issue #35). یعنی یک مقدار نادرست فقط یک
        // فراخوانی با آن فاصله داشت.

        public static async Task SendApproveOrRejectUserToAdminsKeyboard(
     this ITelegramBotClient botClient,
     UserDto user,
     long groupId)
        {
            // 1) لیست ادمین‌ها
            var adminIds = await botClient.GetAdminUserIdsAsync(groupId);

            // 2) متن پیام
            var text =
     $"📌 درخواست عضویت جدید\n\n" +
     $"👤 نام: {Utils.EscapeHtml(user.FirstName)} {Utils.EscapeHtml(user.LastName)}\n" +
     $"🆔 Telegram ID: <code>{user.TelegramId}</code>\n" +
     $"🔖 Username: {Utils.UsernameLink(user.Username)}\n" +
     $"📞 Phone: {Utils.EscapeHtml(user.PhoneNumber ?? "-")}\n" +
     $"📅 ثبت‌نام: <code>{user.CreatedAt:yyyy/MM/dd HH:mm}</code>";

            // 3) ساخت اینلاین کیبورد
            var keyboard = new InlineKeyboardMarkup(new[]
            {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                "✅ تأیید",
                $"approve_{user.TelegramId}"),
            InlineKeyboardButton.WithCallbackData(
                "❌ رد",
                $"reject_{user.TelegramId}")
        }
    });

            // 4) ارسال به هر ادمین
            foreach (var adminId in adminIds)
            {
                try
                {
                    await botClient.SendMessage(
                        chatId: adminId,
                        text: text,
                        parseMode: ParseMode.Html,
                        replyMarkup: keyboard);
                }
                catch (Exception ex)
                {
                    // لاگ خطا برای ادمینی که پیام نتوانست ارسال شود
                    Console.WriteLine($"ارسال به ادمین {adminId} ناموفق: {ex.Message}");
                }
            }
        }
    }

}
