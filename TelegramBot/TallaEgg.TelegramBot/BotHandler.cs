using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Configuration;

namespace TallaEgg.TelegramBot;

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
            // New user - start registration process
            await HandleNewUserAsync(chatId, telegramId, message);
            return;
        }

        // Check if user has phone number
        if (string.IsNullOrEmpty(user?.PhoneNumber))
        {
            await HandlePhoneNumberRequestAsync(chatId, telegramId, message);
            return;
        }

        // User is fully registered - handle main menu
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
                await _botClient.SendTextMessageAsync(chatId, 
                    "سلام! برای استفاده از ربات، لطفاً کد دعوت خود را وارد کنید.\n" +
                    "کد دعوت را به صورت زیر وارد کنید:\n" +
                    "/start [کد_دعوت]\n" +
                    "مثال: /start ABC12345");
            }
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, 
                "برای شروع، لطفاً کد دعوت خود را وارد کنید:\n" +
                "/start [کد_دعوت]");
        }
    }

    private async Task HandleInvitationCodeAsync(long chatId, long telegramId, string invitationCode, Message message)
    {
        // Validate invitation code
        (bool isValid, string strmessage) = await _affiliateApi.ValidateInvitationAsync(invitationCode);
        
        if (!isValid)
        {
            await _botClient.SendTextMessageAsync(chatId, $"خطا: {strmessage}");
            return;
        }

        // Register user
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

        // Use invitation
        (bool useSuccess, string useMessage, Guid? invitationId) = await _affiliateApi.UseInvitationAsync(invitationCode, userId.Value);
        
        if (!useSuccess)
        {
            await _botClient.SendTextMessageAsync(chatId, $"خطا در استفاده از کد دعوت: {useMessage}");
            return;
        }

        // Welcome message and request phone number
        await _botClient.SendTextMessageAsync(chatId, 
            "🎉 خوش آمدید!\n" +
            "ثبت‌نام شما با موفقیت انجام شد.\n\n" +
            "لطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.",
            replyMarkup: new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton("📱 اشتراک‌گذاری شماره تلفن") { RequestContact = true } }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            });
    }

    private async Task HandlePhoneNumberRequestAsync(long chatId, long telegramId, Message message)
    {
        var msgText = message.Text ?? "";

        if (message.Contact != null && !string.IsNullOrEmpty(message.Contact.PhoneNumber))
        {
            var phoneNumber = message.Contact.PhoneNumber;
            if (!phoneNumber.StartsWith("+"))
                phoneNumber = "+" + phoneNumber;

            (bool success, string updateMessage) = await _usersApi.UpdatePhoneAsync(telegramId, phoneNumber);
            
            if (success)
            {
                await _botClient.SendTextMessageAsync(chatId, 
                    "✅ شماره تلفن شما با موفقیت ثبت شد!\n\n" +
                    "حالا می‌توانید از خدمات ربات استفاده کنید.",
                    replyMarkup: new ReplyKeyboardRemove());
                
                await ShowMainMenuAsync(chatId);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, $"خطا در ثبت شماره تلفن: {updateMessage}");
            }
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, 
                "لطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.",
                replyMarkup: new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { new KeyboardButton("📱 اشتراک‌گذاری شماره تلفن") { RequestContact = true } }
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
            case "💰 نقدی":
                await _botClient.SendTextMessageAsync(chatId, "بخش نقدی در حال توسعه است...");
                break;

            case "📈 آتی":
                await HandleFuturesMenuAsync(chatId);
                break;

            case "📊 حسابداری":
                await _botClient.SendTextMessageAsync(chatId, "بخش حسابداری در حال توسعه است...");
                break;

            case "❓ راهنما":
                await ShowHelpAsync(chatId);
                break;

            case "🔙 بازگشت":
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
            new KeyboardButton[] { "💰 نقدی", "📈 آتی" },
            new KeyboardButton[] { "📊 حسابداری", "❓ راهنما" }
        })
        {
            ResizeKeyboard = true
        };

        await _botClient.SendTextMessageAsync(chatId, 
            "🎯 منوی اصلی\n\n" +
            "لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task HandleFuturesMenuAsync(long chatId)
    {
        // Get latest prices
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
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "back_to_main")
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

            case "back_to_main":
                await ShowMainMenuAsync(chatId);
                break;
        }

        // Answer callback query
        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
    }
} 