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
                    new[] { new KeyboardButton(BotTexts.BtnMakeOrderSpot), new KeyboardButton(BotTexts.BtnTakeOrder) },
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

        public static async Task SendAccountingMenuKeyboard(this ITelegramBotClient _botClient, long chatId)
        {

            var keyboard = new ReplyKeyboardMarkup(
               new[]
               {
                    new[] { new KeyboardButton(BotTexts.TradeHistory)},
                    new[] { new KeyboardButton(BotTexts.MainMenu)},
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


        public static async Task SendUserOrdersWithPagingAsync(
    this ITelegramBotClient bot,
    long chatId,
    PagedResult<OrderHistoryDto> page,
    int currentPage,
    Guid userId)
        {
            if (page == null || !page.Items.Any())
            {
                await bot.SendMessage(chatId, "هیچ سفارشی یافت نشد.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📋 *سفارشات شما – صفحه {currentPage} از {page.TotalPages}*\n");

            foreach (var o in page.Items)
            {
                sb.AppendLine(
                    $"📌 *سفارش #{o.Id.ToString()[..8]}…*\n" +
                    $"🏷️ دارایی: *{o.Asset}*\n" +
                    $"🔺 نوع: *{GetTypeIcon(o.Type)} {o.Type}*\n" +
                    $"📊 حجم: *{o.Amount}* @ قیمت *{o.Price:#,0}*\n" +
                    $"📈 بازار: *{o.TradingType}* | نقش: *{o.Role}*\n" +
                    $"⚡ وضعیت: *{GetStatusEmoji(o.Status)} {o.Status}*\n" +
                    $"🕓 ثبت: *{o.CreatedAt:yyyy/MM/dd HH:mm}* " +
                    (o.UpdatedAt.HasValue ? $"| آخرین ویرایش: *{o.UpdatedAt:HH:mm}*" : "") +
                    (!string.IsNullOrWhiteSpace(o.Notes) ? $"\n📝 یادداشت: _{o.Notes}_" : "") +
                    "\n➖➖➖➖➖➖➖➖➖\n");
            }

            // ساخت کیبورد صفحه‌بندی
            var buttons = new List<InlineKeyboardButton>();
            if (currentPage > 1)
                buttons.Add(InlineKeyboardButton.WithCallbackData("⬅️ قبلی", $"orders_{userId}_{currentPage - 1}"));
            if (currentPage < page.TotalPages)
                buttons.Add(InlineKeyboardButton.WithCallbackData("بعدی ➡️", $"orders_{userId}_{currentPage + 1}"));

            var keyboard = buttons.Any()
                ? new InlineKeyboardMarkup(buttons)
                : null;

            await bot.SendMessage(
                chatId: chatId,
                text: sb.ToString(),
                parseMode: ParseMode.None,
                replyMarkup: keyboard);
        }

        // -------------- کمکی -----------------
        private static string GetTypeIcon(OrderType type) => type switch
        {
            OrderType.Buy => "🟢",
            OrderType.Sell => "🔴",
            _ => "⚪"
        };

        private static string GetStatusEmoji(OrderStatus status) => status switch
        {
            OrderStatus.Pending => "⏳",
            OrderStatus.Completed => "✅",
            OrderStatus.Cancelled => "❌",
            OrderStatus.Failed => "⚠️",
            _ => "❓"
        };


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
