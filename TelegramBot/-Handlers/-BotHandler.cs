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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers;

public class OrderState
{
    public string TradingType { get; set; } = ""; // "Spot" or "Futures"
    public string OrderType { get; set; } = ""; // "Buy" or "Sell"
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public Guid UserId { get; set; }
    public bool IsConfirmed { get; set; } = false;
}

public class OrderDto
{
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "Buy";
    public string TradingType { get; set; } = "Spot";
}

public class BotHandler : IBotHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserService _userService;
    private readonly IPriceService _priceService;
    private readonly IOrderService _orderService;
    private readonly ILogger<BotHandler> _logger;
    private readonly Dictionary<long, OrderState> _userOrderStates = new();

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
            else if (text.StartsWith(ButtonTextsConstants.MainMenu))
            {
                await ShowMainMenu(chatId);
            }
            else if (message.Contact != null)
            {
                await HandlePhoneNumber(message);
            }
            else if (text.StartsWith(ButtonTextsConstants.Help, StringComparison.OrdinalIgnoreCase))
            {
                await ShowHelpMenu(chatId);
            }
            else if (text.StartsWith(ButtonTextsConstants.Spot, StringComparison.OrdinalIgnoreCase))
            {
                await ShowSpotMenu(chatId);
            }
            else if (text.StartsWith(ButtonTextsConstants.MakeOrder, StringComparison.OrdinalIgnoreCase))
            {
                await ShowSymbolsList(chatId);
            }
            if (text.StartsWith("asset_"))
            {
                var asset = text.Substring("asset_".Length); // حذف پیشوند "asset_"
                await HandleAssetSelectionAsync(chatId, message.From!.Id, asset);
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
                    await ShowSpotMenu(chatId);
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
                    else if (data?.StartsWith("asset_") == true)
                    {
                        await HandleAssetSelection(chatId, data, callbackQuery);
                    }
                    else if (data?.StartsWith("trading_") == true)
                    {
                        await HandleTradingTypeSelection(chatId, data, callbackQuery);
                    }
                    else if (data?.StartsWith("order_type_") == true)
                    {
                        await HandleOrderTypeSelection(chatId, data, callbackQuery);
                    }
                    else if (data == "confirm_order")
                    {
                        await HandleOrderConfirmation(chatId, callbackQuery);
                    }
                    else if (data == "cancel_order")
                    {
                        await HandleOrderCancellation(chatId, callbackQuery);
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

    private async Task ExceptionHanding(long chatId, Exception ex, string? messge = null)
    {
        _logger.LogError(ex, $"{(string.IsNullOrEmpty(messge) ? string.Empty : messge)}");
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
    private async Task ShowSpotMenu(long chatId)
    {
        await _botClient.SpotMenuKeyboard(chatId);
    }

    /// <summary>
    /// فعلا هیچی پاک نکن
    /// چیزای اضافه بذار باشن به عنوان سمپل برای کپی پیست کردن لازم میشن
    /// </summary>
    /// <param name="chatId"></param>
    /// <returns></returns>
    private async Task ShowAssetsList(long chatId)
    {
        try
        {
            // گرفتن لیست قیمت‌ها از PriceService
            var prices = await _priceService.GetAllPricesAsync();

            if (prices == null || !prices.Any())
            {
                await _botClient.SendTextMessageAsync(chatId,
                    "⚠️ در حال حاضر لیست دارایی‌های قابل معامله در دسترس نیست.\n" +
                    "لطفاً بعداً تلاش کنید.");
                return;
            }

            // ساخت دکمه‌ها برای هر دارایی با نمایش قیمت
            var assetButtons = new List<InlineKeyboardButton[]>();

            foreach (var price in prices)
            {
                var displayText = $"{GetAssetEmoji(price.Asset)} {price.Asset} - {price.BuyPrice:N0} تومان";
                assetButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(displayText, $"asset_{price.Asset}")
                });
            }

            // اضافه کردن دکمه بازگشت
            assetButtons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 بازگشت به منوی اصلی", "back_to_main")
            });

            var keyboard = new InlineKeyboardMarkup(assetButtons);

            // ارسال پیام به کاربر با توضیحات
            var messageText = "📊 **لیست دارایی‌های قابل معامله**\n\n" +
                            "لطفاً دارایی مورد نظر خود را انتخاب کنید:\n" +
                            "قیمت‌ها به صورت لحظه‌ای به‌روزرسانی می‌شوند.";

            await _botClient.SendTextMessageAsync(
                chatId,
                messageText,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در نمایش لیست دارایی‌ها برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در دریافت لیست دارایی‌ها.\n" +
                "لطفاً بعداً تلاش کنید.");
        }
    }

    /// <summary>
    /// نمادهای قابل معامله را به کاربر نمایش میدهد
    /// Trading Pair
    /// مثال: BTCUSDT
    /// </summary>
    /// <param name="chatId"></param>
    /// <returns></returns>
    private async Task ShowSymbolsList(long chatId)
    {
        /// فعلا هارد کد کردم چون یک نماد معاملاتی بیشتر نداریم
        /// ولی بعدا باید یک جدول براش در نطر بگیریم و از سرویس خودش بحونیمش
        try
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("🪙 طلا آبشده", $"asset_Melted"),

                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "back_to_main")
                }
            });

            // ارسال پیام به کاربر با توضیحات
            var messageText = "📊 **لیست دارایی‌های قابل معامله**\n\n" +
                            "لطفاً دارایی مورد نظر خود را انتخاب کنید:\n" +
                            "قیمت‌ها به صورت لحظه‌ای به‌روزرسانی می‌شوند.";

            await _botClient.SendTextMessageAsync(
                chatId,
                messageText,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در نمایش لیست دارایی‌ها برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در دریافت لیست دارایی‌ها.\n" +
                "لطفاً بعداً تلاش کنید.");
        }
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

    private async Task HandleAssetSelection(long chatId, string data, CallbackQuery callbackQuery)
    {
        try
        {
            var asset = data.Substring("asset_".Length); // حذف پیشوند "asset_"
            var telegramId = callbackQuery.From?.Id ?? 0;

            // ذخیره asset در state کاربر
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                _userOrderStates[telegramId] = new OrderState();
            }

            _userOrderStates[telegramId].Asset = asset;

            // دریافت قیمت فعلی
            var price = await _priceService.GetLatestPriceAsync(asset);
            if (price != null)
            {
                _userOrderStates[telegramId].Price = price.BuyPrice;

                // نمایش منوی نوع سفارش (خرید/فروش)
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🛒 خرید", "order_type_buy"),
                        InlineKeyboardButton.WithCallbackData("🛍️ فروش", "order_type_sell")
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "back_to_assets")
                    }
                });

                var messageText = $"📊 **انتخاب نوع سفارش**\n\n" +
                                $"نماد: **{asset}**\n" +
                                $"قیمت فعلی: **{price.BuyPrice:N0}** تومان\n\n" +
                                $"نوع سفارش خود را انتخاب کنید:";

                await _botClient.SendTextMessageAsync(
                    chatId,
                    messageText,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: keyboard);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId,
                    $"❌ خطا در دریافت قیمت {asset}.\n" +
                    "لطفاً دوباره تلاش کنید.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در انتخاب دارایی برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در انتخاب دارایی.\n" +
                "لطفاً دوباره تلاش کنید.");
        }
    }

    private async Task HandleTradingTypeSelection(long chatId, string data, CallbackQuery callbackQuery)
    {
        try
        {
            var tradingType = data.Substring("trading_".Length); // حذف پیشوند "trading_"
            var telegramId = callbackQuery.From?.Id ?? 0;

            // ذخیره trading type در state کاربر
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                _userOrderStates[telegramId] = new OrderState();
            }

            _userOrderStates[telegramId].TradingType = tradingType;

            // نمایش لیست دارایی‌ها
            await ShowAssetsList(chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در انتخاب نوع معامله برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در انتخاب نوع معامله.\n" +
                "لطفاً دوباره تلاش کنید.");
        }
    }

    private async Task HandleOrderTypeSelection(long chatId, string data, CallbackQuery callbackQuery)
    {
        try
        {
            var orderType = data.Substring("order_type_".Length); // حذف پیشوند "order_type_"
            var telegramId = callbackQuery.From?.Id ?? 0;

            if (_userOrderStates.ContainsKey(telegramId))
            {
                _userOrderStates[telegramId].OrderType = orderType;

                // درخواست مقدار واحد
                await _botClient.SendTextMessageAsync(chatId,
                    $"📝 **ثبت سفارش {orderType}**\n\n" +
                    $"نماد: **{_userOrderStates[telegramId].Asset}**\n" +
                    $"قیمت: **{_userOrderStates[telegramId].Price:N0}** تومان\n\n" +
                    "لطفاً مقدار واحد را وارد کنید:",
                    parseMode: ParseMode.Markdown);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId,
                    "❌ خطا در پردازش سفارش.\n" +
                    "لطفاً از ابتدا شروع کنید.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در انتخاب نوع سفارش برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در انتخاب نوع سفارش.\n" +
                "لطفاً دوباره تلاش کنید.");
        }
    }

    private async Task HandleOrderConfirmation(long chatId, CallbackQuery callbackQuery)
    {
        try
        {
            var telegramId = callbackQuery.From?.Id ?? 0;

            if (_userOrderStates.ContainsKey(telegramId))
            {
                var orderState = _userOrderStates[telegramId];

                // بررسی موجودی برای فروش
                if (orderState.OrderType.ToLower() == "sell")
                {
                    var (balanceSuccess, balance) = await _userService.GetUserBalanceAsync(telegramId, orderState.Asset);
                    if (!balanceSuccess || balance < orderState.Amount)
                    {
                        await _botClient.SendTextMessageAsync(chatId,
                            $"❌ موجودی کافی نیست.\n" +
                            $"موجودی شما: **{balance}** واحد\n" +
                            $"مقدار درخواستی: **{orderState.Amount}** واحد",
                            parseMode: ParseMode.Markdown);
                        return;
                    }
                }

                // ثبت سفارش
                try
                {
                    var order = await _orderService.CreateOrderAsync(
                        orderState.Asset,
                        orderState.Amount,
                        orderState.Price,
                        orderState.UserId,
                        orderState.OrderType
                    );
                    var success = order != null;
                    var message = success ? "سفارش با موفقیت ثبت شد" : "خطا در ثبت سفارش";

                    if (success)
                    {
                        await _botClient.SendTextMessageAsync(chatId,
                            $"✅ **سفارش با موفقیت ثبت شد!**\n\n" +
                            $"نماد: **{orderState.Asset}**\n" +
                            $"نوع: **{orderState.OrderType}**\n" +
                            $"مقدار: **{orderState.Amount}** واحد\n" +
                            $"قیمت: **{orderState.Price:N0}** تومان\n" +
                            $"مبلغ کل: **{orderState.Amount * orderState.Price:N0}** تومان",
                            parseMode: ParseMode.Markdown);

                        // پاک کردن state
                        _userOrderStates.Remove(telegramId);
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(chatId,
                            $"❌ خطا در ثبت سفارش: {message}");
                    }
                }
                catch (Exception ex)
                {
                    await _botClient.SendTextMessageAsync(chatId,
                        $"❌ خطا در ثبت سفارش: {ex.Message}");
                }
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId,
                    "❌ خطا در پردازش سفارش.\n" +
                    "لطفاً از ابتدا شروع کنید.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در تایید سفارش برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در تایید سفارش.\n" +
                "لطفاً دوباره تلاش کنید.");
        }
    }

    private async Task HandleOrderCancellation(long chatId, CallbackQuery callbackQuery)
    {
        try
        {
            var telegramId = callbackQuery.From?.Id ?? 0;

            // پاک کردن state
            if (_userOrderStates.ContainsKey(telegramId))
            {
                _userOrderStates.Remove(telegramId);
            }

            await _botClient.SendTextMessageAsync(chatId,
                "❌ سفارش لغو شد.\n" +
                "می‌توانید سفارش جدیدی ثبت کنید.");

            // نمایش منوی اصلی
            await ShowMainMenu(chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در لغو سفارش برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در لغو سفارش.\n" +
                "لطفاً دوباره تلاش کنید.");
        }
    }

    private async Task HandleAssetSelectionAsync(long chatId, long telegramId, string asset)
    {
        try
        {
            // ذخیره asset در state کاربر
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                _userOrderStates[telegramId] = new OrderState();
            }

            _userOrderStates[telegramId].Asset = asset;

            // دریافت قیمت فعلی
            var price = await _priceService.GetLatestPriceAsync(asset);
            if (price != null)
            {
                _userOrderStates[telegramId].Price = price.BuyPrice;

                // نمایش منوی نوع سفارش (خرید/فروش)
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🛒 خرید", "order_type_buy"),
                        InlineKeyboardButton.WithCallbackData("🛍️ فروش", "order_type_sell")
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "back_to_assets")
                    }
                });

                var messageText = $"📊 **انتخاب نوع سفارش**\n\n" +
                                $"نماد: **{asset}**\n" +
                                $"قیمت فعلی: **{price.BuyPrice:N0}** تومان\n\n" +
                                $"نوع سفارش خود را انتخاب کنید:";

                await _botClient.SendTextMessageAsync(
                    chatId,
                    messageText,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: keyboard);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId,
                    $"❌ خطا در دریافت قیمت {asset}.\n" +
                    "لطفاً دوباره تلاش کنید.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در انتخاب دارایی برای chatId: {ChatId}", chatId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ خطا در انتخاب دارایی.\n" +
                "لطفاً دوباره تلاش کنید.");
        }
    }

    private string GetAssetEmoji(string asset)
    {
        return asset.ToLower() switch
        {
            "gold" or "طلا" => "🪙",
            "diamond" or "الماس" => "💎",
            "silver" or "نقره" => "🥈",
            "platinum" or "پلاتین" => "⚪",
            "bitcoin" or "بیت‌کوین" => "₿",
            "ethereum" or "اتریوم" => "Ξ",
            _ => "��"
        };
    }
}