using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.RegularExpressions;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;
using TallaEgg.TelegramBot.Infrastructure.Keyboards.ReplyKeyboards;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers;

public class BotHandler : IBotHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserService _userService;
    private readonly IPriceService _priceService;
    private readonly IOrderService _orderService;
    private readonly ILogger<BotHandler> _logger;

    public BotHandler(
        ITelegramBotClient botClient,
        IUserService userService,
        IPriceService priceService,
        IOrderService orderService,
        ILogger<BotHandler> logger)
    {
        _botClient = botClient;
        _userService = userService;
        _priceService = priceService;
        _orderService = orderService;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(object updateObj)
    {
        try
        {
            var update = (Update)updateObj;
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageAsync(update.Message!);
                    break;
                case UpdateType.CallbackQuery:
                    await HandleCallbackQueryAsync(update.CallbackQuery!);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    public async Task HandleMessageAsync(object messageObj)
    {
        var message = (Message)messageObj;
        if (message.Text == null) message.Text = "";

        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();

        try
        {
            if (text.StartsWith("/start"))
            {
                await HandleStartCommand(message);
            }
            else if (text.StartsWith("/menu"))
            {
                await ShowMainMenu(chatId);
            }
            else if (message.Contact != null)
            {
                await HandlePhoneNumber(message);
            }
            else if( text.StartsWith(ButtonTextsConstants.Help, StringComparison.OrdinalIgnoreCase))
            {
                await ShowHelpMenu(chatId);
            }
            else if (text.StartsWith(ButtonTextsConstants.Spot, StringComparison.OrdinalIgnoreCase))
            {
                await ShowCashMenu(chatId);
            }
            else if (text.StartsWith(ButtonTextsConstants.Future, StringComparison.OrdinalIgnoreCase))
            {
                await ShowFuturesMenu(chatId);
            }
            else if (text.StartsWith(ButtonTextsConstants.Accounting, StringComparison.OrdinalIgnoreCase))
            {
                await ShowAccountingMenu(chatId);
            }
            else if (text.StartsWith(ButtonTextsConstants.Wallet, StringComparison.OrdinalIgnoreCase))
            {
                await ShowWalletMenu(chatId);
            }
            else if (text.StartsWith(ButtonTextsConstants.History, StringComparison.OrdinalIgnoreCase))
            {
                await ShowHistoryMenu(chatId);
            }
            else
            {
                await HandleTextMessage(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            await _botClient.SendTextMessageAsync(chatId, "خطایی رخ داد. لطفاً دوباره تلاش کنید.");
        }
    }

    public async Task HandleCallbackQueryAsync(object callbackQueryObj)
    {
        var callbackQuery = (CallbackQuery)callbackQueryObj;
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data;

        try
        {
            switch (data)
            {
                case "menu_main":
                    await ShowMainMenu(chatId);
                    break;
                case "menu_cash":
                    await ShowCashMenu(chatId);
                    break;
                case "menu_futures":
                    await ShowFuturesMenu(chatId);
                    break;
                case "menu_accounting":
                    await ShowAccountingMenu(chatId);
                    break;
                case "menu_help":
                    await ShowHelpMenu(chatId);
                    break;
                case "menu_wallet":
                    await ShowWalletMenu(chatId);
                    break;
                case "menu_history":
                    await ShowHistoryMenu(chatId);
                    break;
                case "back_to_main":
                    await ShowMainMenu(chatId);
                    break;
                default:
                    if (data?.StartsWith("price_") == true)
                    {
                        await HandlePriceSelection(chatId, data);
                    }
                    else if (data?.StartsWith("order_") == true)
                    {
                        await HandleOrderSelection(chatId, data);
                    }
                    else if (data?.StartsWith("placeorder_") == true)
                    {
                        var asset = data.Replace("placeorder_", "");
                        await AskOrderAmount(chatId, asset);
                    }
                    break;
            }

            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback query");
            await _botClient.SendTextMessageAsync(chatId, "خطایی رخ داد. لطفاً دوباره تلاش کنید.");
        }
    }

    private async Task HandleStartCommand(Message message)
    {
        var chatId = message.Chat.Id;
        var text = message.Text ?? "";
        var parts = text.Split('?', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            await _botClient.SendTextMessageAsync(chatId, 
                "لطفاً کد دعوت خود را وارد کنید:\n" +
                "/start?[کد_دعوت]");
            return;
        }

        var invitationCode = parts[1];
        var user = await _userService.GetUserByTelegramIdAsync(message.From!.Id);

        // یوزرقبلا ثبت نام کرده
        if (user != null)
        {
            if (!user.IsActive) await _botClient.RequestContactKeyboard(chatId);
            else await ShowMainMenu(chatId);
            return;
        }

        //var result = await _userService.ValidateInvitationCodeAsync(invitationCode);
        //var isValid = result.isValid;
        //var messageText = result.message;
        
        //if (!isValid)
        //{
        //    await _botClient.SendTextMessageAsync(chatId, messageText);
        //    return;
        //}

        try
        {
            user = await _userService.RegisterUserAsync(
                message.From.Id,
                message.From.Username,
                message.From.FirstName,
                message.From.LastName,
                invitationCode);

            await _botClient.RequestContactKeyboard(chatId);
        }
        catch (Exception ex)
        {
            await ExceptionHanding(chatId, ex, "Error registering user");
        }
    }

    private async Task ExceptionHanding(long chatId, Exception ex,string? messge=null)
    {
        _logger.LogError(ex, $"{(string.IsNullOrEmpty(messge)? string.Empty:messge)}");
        await _botClient.SendTextMessageAsync(Constants.DeveloperChatId, JsonSerializer.Serialize(ex));
        await _botClient.SendTextMessageAsync(chatId, Constants.SupportErrorMessage);
    }

    private async Task HandlePhoneNumber(Message message)
    {
        var chatId = message.Chat.Id;
        var phoneNumber = message.Contact!.PhoneNumber;


        if (phoneNumber.StartsWith("98"))//98938621990
        {
            phoneNumber = phoneNumber.Replace("98", "0");
        }
        if (phoneNumber.StartsWith("+98"))//98938621990
        {
            phoneNumber = phoneNumber.Replace("+98", "0");
        }

        try
        {
           await _userService.UpdateUserPhoneAsync(message.From!.Id, phoneNumber);
            await _botClient.SendTextMessageAsync(chatId, 
                "شماره تلفن با موفقیت ثبت شد! ✅\n" +
                "حالا می‌توانید از خدمات ما استفاده کنید.");
            
            await ShowMainMenu(chatId);
        }
        catch (Exception ex)
        {
            await ExceptionHanding(chatId, ex, "Error updating phone number");
        }
    }

    private async Task HandleTextMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var text = message.Text ?? "";

        // Check if user exists and has phone number
        var user = await _userService.GetUserByTelegramIdAsync(message.From!.Id);
        if (user == null || string.IsNullOrEmpty(user.PhoneNumber))
        {
            await _botClient.SendTextMessageAsync(chatId, 
                "لطفاً ابتدا ثبت‌نام کنید و شماره تلفن خود را وارد کنید.");
            return;
        }

        // Handle different text commands
        switch (text.ToLower())
        {
            case "قیمت":
            case "price":
                await ShowPriceMenu(chatId);
                break;
            case "سفارش":
            case "order":
                await ShowOrderMenu(chatId);
                break;
            default:
                await _botClient.SendTextMessageAsync(chatId, 
                    "لطفاً از منوی اصلی استفاده کنید.");
                break;
        }
    }

    private async Task ShowMainMenu(long chatId)
    {
        await _botClient.MainMenuKeyboard(chatId);
    }

    private async Task ShowCashMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🪙 طلا", "price_gold"),
                InlineKeyboardButton.WithCallbackData("💎 الماس", "price_diamond")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            "💰 معاملات نقدی\n" +
            "لطفاً دارایی مورد نظر خود را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task ShowFuturesMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🪙 طلا آتی", "price_gold_futures"),
                InlineKeyboardButton.WithCallbackData("💎 الماس آتی", "price_diamond_futures")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            "📈 معاملات آتی\n" +
            "لطفاً دارایی مورد نظر خود را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task ShowAccountingMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("💰 موجودی", "account_balance"),
                InlineKeyboardButton.WithCallbackData("📋 تاریخچه", "account_history")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            "📊 حسابداری\n" +
            "لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task ShowHelpMenu(long chatId)
    {
        var helpText = 
            "❓ راهنمای استفاده\n\n" +
            "🔹 برای شروع معامله:\n" +
            "1. منوی نقدی یا آتی را انتخاب کنید\n" +
            "2. دارایی مورد نظر را انتخاب کنید\n" +
            "3. قیمت‌ها را مشاهده کنید\n" +
            "4. دکمه خرید یا فروش را بزنید\n\n" +
            "🔹 برای مشاهده موجودی:\n" +
            "منوی حسابداری را انتخاب کنید\n\n" +
            "🔹 برای پشتیبانی:\n" +
            "با ادمین تماس بگیرید";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId, helpText, replyMarkup: keyboard);
    }

    private async Task ShowWalletMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("💰 موجودی", "wallet_balance"),
                InlineKeyboardButton.WithCallbackData("💸 واریز", "wallet_deposit")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("💳 برداشت", "wallet_withdraw"),
                InlineKeyboardButton.WithCallbackData("📊 تراکنشات", "wallet_transactions")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            "💳 کیف پول\n" +
            "لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task ShowHistoryMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("📋 سفارشات", "history_orders"),
                InlineKeyboardButton.WithCallbackData("💰 معاملات", "history_trades")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("💳 تراکنشات", "history_transactions"),
                InlineKeyboardButton.WithCallbackData("📊 گزارش", "history_report")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            "📋 تاریخچه\n" +
            "لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task ShowPriceMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🪙 طلا", "price_gold"),
                InlineKeyboardButton.WithCallbackData("💎 الماس", "price_diamond")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            "💰 قیمت‌ها\n" +
            "لطفاً دارایی مورد نظر خود را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task ShowOrderMenu(long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🪙 طلا", "order_gold"),
                InlineKeyboardButton.WithCallbackData("💎 الماس", "order_diamond")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            "📋 سفارشات\n" +
            "لطفاً دارایی مورد نظر خود را انتخاب کنید:",
            replyMarkup: keyboard);
    }

    private async Task HandlePriceSelection(long chatId, string data)
    {
        var asset = data.Replace("price_", "");
        var price = await _priceService.GetLatestPriceAsync(asset);

        if (price == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "قیمت برای این دارایی در دسترس نیست.");
            return;
        }

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData($"🟢 خرید {asset}", $"order_{asset}_buy"),
                InlineKeyboardButton.WithCallbackData($"🔴 فروش {asset}", $"order_{asset}_sell")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("📝 ثبت سفارش", $"placeorder_{asset}"),
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        var message = $"💰 قیمت {asset}\n\n" +
                     $"🟢 قیمت خرید: {price.BuyPrice:N0} تومان\n" +
                     $"🔴 قیمت فروش: {price.SellPrice:N0} تومان\n" +
                     $"🕐 آخرین به‌روزرسانی: {price.UpdatedAt:HH:mm}";

        await _botClient.SendTextMessageAsync(chatId, message, replyMarkup: keyboard);
    }

    private async Task HandleOrderSelection(long chatId, string data)
    {
        var parts = data.Split('_');
        if (parts.Length < 3) return;

        var asset = parts[1];
        var orderType = parts[2];

        var price = await _priceService.GetLatestPriceAsync(asset);
        if (price == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "قیمت برای این دارایی در دسترس نیست.");
            return;
        }

        var orderPrice = orderType == "buy" ? price.BuyPrice : price.SellPrice;
        var orderTypeText = orderType == "buy" ? "خرید" : "فروش";

        // ذخیره اطلاعات سفارش در session (در حالت واقعی باید از cache یا database استفاده شود)
        var orderInfo = new
        {
            Asset = asset,
            Type = orderType,
            Price = orderPrice,
            ChatId = chatId
        };

        var message = $"📋 سفارش {orderTypeText} {asset}\n\n" +
                     $"💰 قیمت: {orderPrice:N0} تومان\n" +
                     $"📅 تاریخ: {DateTime.Now:yyyy/MM/dd}\n" +
                     $"⏰ ساعت: {DateTime.Now:HH:mm}\n\n" +
                     $"لطفاً تعداد واحد مورد نظر خود را وارد کنید:";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main")
            }
        });

        await _botClient.SendTextMessageAsync(chatId, message, replyMarkup: keyboard);
    }

    // متد جدید: پرسیدن مقدار سفارش از کاربر
    private async Task AskOrderAmount(long chatId, string asset)
    {
        await _botClient.SendTextMessageAsync(chatId, $"لطفاً مقدار {asset} مورد نظر برای ثبت سفارش را وارد کنید:");
        // در اینجا باید مقدار وارد شده را در پیام بعدی کاربر دریافت و سفارش را ثبت کنید (مثلاً با ذخیره وضعیت کاربر در cache یا database)
        // برای سادگی، فرض کنید پیام بعدی کاربر مقدار است و سفارش ثبت می‌شود:
        // این منطق باید در HandleTextMessage تکمیل شود.
    }

    // در متد HandleTextMessage، اگر وضعیت کاربر در حالت "در حال ثبت سفارش" بود، سفارش را ثبت کن
    // اینجا فقط نمونه ساده:
    /*
    private Dictionary<long, string> _pendingOrderAsset = new(); // chatId -> asset

    private async Task HandleTextMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var text = message.Text ?? "";

        if (_pendingOrderAsset.TryGetValue(chatId, out var asset))
        {
            if (decimal.TryParse(text, out var amount) && amount > 0)
            {
                // فرض: قیمت را از سرویس قیمت بگیر و سفارش ثبت کن
                var price = await _priceService.GetLatestPriceAsync(asset);
                var user = await _userService.GetUserByTelegramIdAsync(message.From!.Id);
                if (user != null && price != null)
                {
                    await _orderService.CreateOrderAsync(asset, amount, price.BuyPrice, user.Id, "BUY");
                    await _botClient.SendTextMessageAsync(chatId, "✅ سفارش شما ثبت شد.");
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, "خطا در ثبت سفارش.");
                }
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "مقدار وارد شده نامعتبر است.");
            }
            _pendingOrderAsset.Remove(chatId);
            return;
        }

        // ...existing code...
    }
    */
    // برای پیاده‌سازی کامل، باید وضعیت کاربر را مدیریت کنید.
}