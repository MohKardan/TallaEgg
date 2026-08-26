using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.TelegramBot.Core.Utilties;
using PersianFormat = TallaEgg.Core.Utilties.PersianFormat;
using Telegram.Bot;
using TallaEgg.TelegramBot.Infrastructure.Messaging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram
{
    public static class Keyboards
    {
        public static async Task RequestContactKeyboard(this IBotMessenger _botClient, long chatId)
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


            await _botClient.SendAsync(
                chatId,
                "برای ثبت نام شماره خود را از طریق کلید زیر ارسال کنید",
            replyMarkup: keyboard);

        }

        // MainMenuKeyboard اینجا حذف شد: از هیچ‌جا صدا زده نمی‌شد و تنها جایی بود که دکمهٔ
        // «📈 آتی» را نشان می‌داد — دکمه‌ای برای بازاری که وجود ندارد و هیچ handlerی هم
        // نداشت. منوی اصلیِ واقعی جای دیگری ساخته می‌شود.

        public static async Task SendContactKeyboardAsync(this IBotMessenger _botClient, long chatId)
        {
            var sharePhoneButton = new KeyboardButton(BotBtns.BtnSharePhone) { RequestContact = true };

            var keyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { sharePhoneButton }
                    })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendAsync(
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
        public static async Task SendMainKeyboardForAdminAsync(this IBotMessenger _botClient, long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                //new KeyboardButton[] { new KeyboardButton(BotBtns.BtnSpotCreateOrder) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnSpotSubmitPrice) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnAccounting) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnHelp) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendAsync(chatId, BotMsgs.MsgMainMenu, replyMarkup: keyboard);
        }
        /// <summary>
        /// منوی اصلی برای کاربر عادی و مدیر فرق میکنه
        /// </summary>
        /// <param name="_botClient"></param>
        /// <param name="chatId"></param>
        /// <returns></returns>
        public static async Task SendMainKeyboardForUserAsync(this IBotMessenger _botClient, long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnSpotMarket) },
                new KeyboardButton[] { new KeyboardButton(BotBtns.BtnHelp), new KeyboardButton(BotBtns.BtnAccounting) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendAsync(chatId, BotMsgs.MsgMainMenu, replyMarkup: keyboard);
        }

        /// <summary>
        /// The customer's accounting menu.
        ///
        /// No quote history here: it is the record of the prices the shop set, including its
        /// margin, which is the shop's business and not the customer's. A customer's own
        /// accounting is the trades they made.
        /// </summary>
        public static async Task SendAccountingMenuKeyboard(this IBotMessenger _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
                new[]
               {
                    new[] { new KeyboardButton(BotBtns.BtnTradeHistory), new KeyboardButton(BotBtns.BtnWalletsBalance)},
                    new[] { new KeyboardButton(BotBtns.BtnMainMenu)},
               })
            {
                ResizeKeyboard = true,
            };


            await _botClient.SendAsync(
                chatId,
                "📑 منوی حسابداری\n" +
                "لطفاً یکی از گزینه‌های را انتخاب کنید:",
            replyMarkup: keyboard);

        }
        public static async Task SendAccountingMenuKeyboardForAdmin(this IBotMessenger _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
               new[]
               {
                    new[] { new KeyboardButton(BotBtns.BtnTradeHistory), new KeyboardButton(BotBtns.BtnQuoteHistory)},
                    new[] { new KeyboardButton(BotBtns.BtnMainMenu)},
               }
                            )
            {
                ResizeKeyboard = true,
            };


            await _botClient.SendAsync(
                chatId,
                "📑 منوی حسابداری\n" +
                "لطفاً یکی از گزینه‌های را انتخاب کنید:",
            replyMarkup: keyboard);

        }
        public static async Task SendSpotSideMenuKeyboard(this IBotMessenger _botClient, long chatId)
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

            await _botClient.SendAsync(chatId, "📈 معاملات نقدی\n\nلطفاً نوع معامله خود را انتخاب کنید:", replyMarkup: keyboard);
        }

        // SendUserOrdersWithPagingAsync و کمکی‌هایش (GetTypeIcon/GetStatusEmoji) حذف شدند.
        //
        // از هیچ‌جا صدا زده نمی‌شد — نسخهٔ زندهٔ فهرست سفارش‌ها در OrderListHandler است.
        // این نسخهٔ متروک تنها جایی بود که o.Role را به کاربر نشان می‌داد، مقداری که
        // همیشه Maker است و درست نیست (issue #35). یعنی یک مقدار نادرست فقط یک
        // فراخوانی با آن فاصله داشت.

        /// <summary>
        /// Notifies every group admin that a user is waiting for approval.
        ///
        /// Takes the admin ids rather than looking them up: the lookup needs the raw
        /// Telegram client, which IBotMessenger deliberately does not expose, and keeping
        /// the two apart means this — the part that decides what each admin sees — can be
        /// tested with a fake messenger.
        /// </summary>
        public static async Task SendApproveOrRejectUserToAdminsKeyboard(
            this IBotMessenger messenger,
            IReadOnlyCollection<long> adminIds,
            UserDto user)
        {
            // Joined from the parts that are actually present. EscapeHtml turns an empty value
            // into "-", so a person with no surname used to be announced as "Mohammad -".
            var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => Utils.EscapeHtml(part)));

            if (string.IsNullOrWhiteSpace(fullName))
                fullName = "-";

            // Numbers are wrapped so they read left-to-right inside a right-to-left message and
            // are shown in Persian digits, as every other number the bot displays is.
            var text = string.Format(
                BotMsgs.MsgMembershipRequest,
                fullName,
                PersianFormat.Ltr(PersianFormat.ToPersianDigits(user.PhoneNumber ?? "-")),
                Utils.UsernameLink(user.Username),
                // No <code> wrapper. Telegram lays a code entity out left-to-right on its own,
                // which fought with the surrounding right-to-left line and left this one field
                // looking scrambled while the phone number beside it — same kind of value,
                // no wrapper — read correctly.
                PersianFormat.Ltr(PersianFormat.ToPersianDigits(user.TelegramId.ToString(CultureInfo.InvariantCulture))),
                PersianFormat.DateTimeText(user.CreatedAt));

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ تأیید", $"approve_{user.TelegramId}"),
                    InlineKeyboardButton.WithCallbackData("❌ رد", $"reject_{user.TelegramId}")
                }
            });

            // Never ask somebody to decide about themselves. An administrator who registers is
            // in this list, so the card about their own account came straight back to them with
            // buttons approving or rejecting themselves.
            //
            // Filtered here rather than at the call site because it is a property of the card,
            // not of one caller: any future place that sends it would have to remember.
            foreach (var adminId in adminIds.Where(id => id != user.TelegramId))
            {
                try
                {
                    await messenger.SendAsync(adminId, text, keyboard, ParseMode.Html);
                }
                catch (Exception ex)
                {
                    // One unreachable admin must not stop the others from being told.
                    Console.WriteLine($"ارسال به ادمین {adminId} ناموفق: {ex.Message}");
                }
            }
        }
    }

}
