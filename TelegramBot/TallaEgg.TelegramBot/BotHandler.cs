using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Configuration;

namespace TallaEgg.TelegramBot
{
    public static class BotTexts
    {
        public const string BtnCash = "💰 نقدی";
        public const string BtnFutures = "📈 آتی";
        public const string BtnAccounting = "📊 حسابداری";
        public const string BtnHelp = "❓ راهنما";
        public const string BtnBack = "🔙 بازگشت";
        public const string BtnSharePhone = "📱 اشتراک‌گذاری شماره تلفن";
        public const string MsgEnterInvite = "برای شروع، لطفاً کد دعوت خود را وارد کنید:\n/start [کد_دعوت]";
        public const string MsgPhoneRequest = "لطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgWelcome = "🎉 خوش آمدید!\nثبت‌نام شما با موفقیت انجام شد.\n\nلطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgPhoneSuccess = "✅ شماره تلفن شما با موفقیت ثبت شد!\n\nحالا می‌توانید از خدمات ربات استفاده کنید.";
        public const string MsgMainMenu = "🎯 منوی اصلی\n\nلطفاً یکی از گزینه‌های زیر را انتخاب کنید:";
    }

    public class BotHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly OrderApiClient _orderApi;
        private readonly UsersApiClient _usersApi;
        private readonly AffiliateApiClient _affiliateApi;
        private readonly PriceApiClient _priceApi;

        public BotHandler(ITelegramBotClient botClient, OrderApiClient orderApi, UsersApiClient usersApi, 
                         AffiliateApiClient affiliateApi, PriceApiClient priceApi)
        {
            _botClient = botClient;
            _orderApi = orderApi;
            _usersApi = usersApi;
            _affiliateApi = affiliateApi;
            _priceApi = priceApi;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            if (update.Message is not { } message || message.Text is not { } msgText)
                return;

            var chatId = message.Chat.Id;
            var telegramId = message.From?.Id ?? 0;

            // Check if user exists
            var (userExists, user) = await _usersApi.GetUserAsync(telegramId);

            if (!userExists)
            {
                await HandleNewUserAsync(chatId, telegramId, message);
                return;
            }

            if (string.IsNullOrEmpty(user?.PhoneNumber))
            {
                await HandlePhoneNumberRequestAsync(chatId, telegramId, message);
                return;
            }

            await HandleMainMenuAsync(chatId, telegramId, message);
        }

        private async Task HandleNewUserAsync(long chatId, long telegramId, Message message)
        {
            var msgText = message.Text ?? "";

            if (msgText.StartsWith("/start"))
            {
                var parts = msgText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    var invitationCode = parts[1];
                    await HandleInvitationCodeAsync(chatId, telegramId, invitationCode, message);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgEnterInvite);
                }
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgEnterInvite);
            }
        }

        private async Task HandleInvitationCodeAsync(long chatId, long telegramId, string invitationCode, Message message)
        {
            (bool isValid, string strmessage) = await _affiliateApi.ValidateInvitationAsync(invitationCode);

            if (!isValid)
            {
                await _botClient.SendTextMessageAsync(chatId, $"خطا: {strmessage}");
                return;
            }

            (bool success, string regMessage, Guid? userId) = await _usersApi.RegisterUserAsync(
                telegramId,
                message.From?.Username,
                message.From?.FirstName,
                message.From?.LastName);

            if (!success)
            {
                await _botClient.SendTextMessageAsync(chatId, $"خطا در ثبت‌نام: {regMessage}");
                return;
            }

            (bool useSuccess, string useMessage, Guid? invitationId) = await _affiliateApi.UseInvitationAsync(invitationCode, userId.Value);

            if (!useSuccess)
            {
                await _botClient.SendTextMessageAsync(chatId, $"خطا در استفاده از کد دعوت: {useMessage}");
                return;
            }

            await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgWelcome,
                replyMarkup: new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { new KeyboardButton(BotTexts.BtnSharePhone) { RequestContact = true } }
                })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                });
        }

        private async Task HandlePhoneNumberRequestAsync(long chatId, long telegramId, Message message)
        {
            if (message.Contact != null && !string.IsNullOrEmpty(message.Contact.PhoneNumber))
            {
                var phoneNumber = message.Contact.PhoneNumber.StartsWith("+") ? message.Contact.PhoneNumber : "+" + message.Contact.PhoneNumber;
                (bool success, string updateMessage) = await _usersApi.UpdatePhoneAsync(telegramId, phoneNumber);
                if (success)
                {
                    await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgPhoneSuccess, replyMarkup: new ReplyKeyboardRemove());
                    await ShowMainMenuAsync(chatId);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, $"خطا در ثبت شماره تلفن: {updateMessage}");
                }
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgPhoneRequest,
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { new KeyboardButton(BotTexts.BtnSharePhone) { RequestContact = true } }
                    })
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    });
            }
        }

        private async Task HandleMainMenuAsync(long chatId, long telegramId, Message message)
        {
            var msgText = message.Text ?? "";

            switch (msgText)
            {
                case BotTexts.BtnCash:
                    await _botClient.SendTextMessageAsync(chatId, "بخش نقدی در حال توسعه است...");
                    break;

                case BotTexts.BtnFutures:
                    await HandleFuturesMenuAsync(chatId);
                    break;

                case BotTexts.BtnAccounting:
                    await _botClient.SendTextMessageAsync(chatId, "بخش حسابداری در حال توسعه است...");
                    break;

                case BotTexts.BtnHelp:
                    await ShowHelpAsync(chatId);
                    break;

                case BotTexts.BtnBack:
                    await ShowMainMenuAsync(chatId);
                    break;

                default:
                    await ShowMainMenuAsync(chatId);
                    break;
            }
        }

        private async Task ShowMainMenuAsync(long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { BotTexts.BtnCash, BotTexts.BtnFutures },
                new KeyboardButton[] { BotTexts.BtnAccounting, BotTexts.BtnHelp }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgMainMenu, replyMarkup: keyboard);
        }

        private async Task HandleFuturesMenuAsync(long chatId)
        {
            var (success, prices) = await _priceApi.GetAllPricesAsync();

            if (!success || prices == null || !prices.Any())
            {
                await _botClient.SendTextMessageAsync(chatId, "در حال حاضر قیمت‌ها در دسترس نیست.");
                return;
            }

            var priceMessage = "📈 آخرین قیمت‌های خرید و فروش:\n\n";
            foreach (var price in prices)
            {
                priceMessage += $"🪙 {price.Asset}:\n";
                priceMessage += $"   خرید: {price.BuyPrice:N0} تومان\n";
                priceMessage += $"   فروش: {price.SellPrice:N0} تومان\n";
                priceMessage += $"   آخرین به‌روزرسانی: {price.UpdatedAt:HH:mm}\n\n";
            }

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("🛒 خرید", "buy_futures"),
                    InlineKeyboardButton.WithCallbackData("🛍️ فروش", "sell_futures")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendTextMessageAsync(chatId, priceMessage, replyMarkup: keyboard);
        }

        private async Task ShowHelpAsync(long chatId)
        {
            var helpText = "❓ راهنمای استفاده از ربات:\n\n" +
                          "💰 نقدی: معاملات نقدی و فوری\n" +
                          "📈 آتی: معاملات آتی و قراردادهای آتی\n" +
                          "📊 حسابداری: مشاهده موجودی و تاریخچه معاملات\n" +
                          "❓ راهنما: این صفحه\n\n" +
                          "برای پشتیبانی با تیم فنی تماس بگیرید.";

            await _botClient.SendTextMessageAsync(chatId, helpText);
        }

        private async Task HandleChargeWalletAsync(long chatId, long telegramId)
        {
            var (userExists, user) = await _usersApi.GetUserAsync(telegramId);
            if (!userExists || user == null)
            {
                await _botClient.SendTextMessageAsync(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
                return;
            }

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("💳 کارت بانکی", "charge_card"),
                    InlineKeyboardButton.WithCallbackData("🏦 بانک", "charge_bank")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendTextMessageAsync(chatId,
                "💳 شارژ کیف پول\n\n" +
                "لطفاً روش پرداخت خود را انتخاب کنید:\n\n" +
                "💳 کارت بانکی: شارژ از طریق کارت بانکی\n" +
                "🏦 بانک: واریز به حساب بانکی",
                replyMarkup: keyboard);
        }

        public async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message?.Chat.Id ?? 0;
            var data = callbackQuery.Data ?? "";

            switch (data)
            {
                case "buy_futures":
                    await _botClient.SendTextMessageAsync(chatId, "بخش خرید آتی در حال توسعه است...");
                    break;

                case "sell_futures":
                    await _botClient.SendTextMessageAsync(chatId, "بخش فروش آتی در حال توسعه است...");
                    break;

                case "charge_card":
                    await _botClient.SendTextMessageAsync(chatId,
                        "💳 شارژ از طریق کارت بانکی\n\n" +
                        "لطفاً مبلغ مورد نظر را وارد کنید (به تومان):\n" +
                        "مثال: 100000");
                    break;

                case "charge_bank":
                    await _botClient.SendTextMessageAsync(chatId,
                        "🏦 واریز به حساب بانکی\n\n" +
                        "شماره حساب: 1234567890\n" +
                        "شماره کارت: 1234-5678-9012-3456\n" +
                        "به نام: شرکت تالا\n\n" +
                        "پس از واریز، رسید را برای ما ارسال کنید.");
                    break;

                case "back_to_main":
                    await ShowMainMenuAsync(chatId);
                    break;
            }

            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }
    }
}