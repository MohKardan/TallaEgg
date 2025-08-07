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
        public const string BtnPlaceOrder = "📝 ثبت سفارش";
        public const string BtnBuy = "🛒 خرید";
        public const string BtnSell = "🛍️ فروش";
        public const string MsgEnterInvite = "برای شروع، لطفاً کد دعوت خود را وارد کنید:\n/start [کد_دعوت]";
        public const string MsgPhoneRequest = "لطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgWelcome = "🎉 خوش آمدید!\nثبت‌نام شما با موفقیت انجام شد.\n\nلطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgPhoneSuccess = "✅ شماره تلفن شما با موفقیت ثبت شد!\n\nحالا می‌توانید از خدمات ربات استفاده کنید.";
        public const string MsgMainMenu = "🎯 منوی اصلی\n\nلطفاً یکی از گزینه‌های زیر را انتخاب کنید:";
        public const string MsgSelectOrderType = "نوع سفارش خود را انتخاب کنید:";
        public const string MsgEnterAmount = "لطفاً مقدار واحد را وارد کنید:";
        public const string MsgInsufficientBalance = "موجودی کافی نیست. موجودی شما: {0} واحد";
        public const string MsgOrderSuccess = "✅ سفارش شما با موفقیت ثبت شد!";
        public const string MsgOrderFailed = "❌ خطا در ثبت سفارش: {0}";
    }

    public class OrderState
    {
        public string OrderType { get; set; } = ""; // "BUY" or "SELL"
        public string Asset { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public Guid UserId { get; set; }
    }

    public class BotHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly OrderApiClient _orderApi;
        private readonly UsersApiClient _usersApi;
        private readonly AffiliateApiClient _affiliateApi;
        private readonly PriceApiClient _priceApi;
        private readonly WalletApiClient _walletApi;
        private readonly Dictionary<long, OrderState> _userOrderStates = new();

        public BotHandler(ITelegramBotClient botClient, OrderApiClient orderApi, UsersApiClient usersApi, 
                         AffiliateApiClient affiliateApi, PriceApiClient priceApi, WalletApiClient walletApi)
        {
            _botClient = botClient;
            _orderApi = orderApi;
            _usersApi = usersApi;
            _affiliateApi = affiliateApi;
            _priceApi = priceApi;
            _walletApi = walletApi;
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

                case BotTexts.BtnPlaceOrder:
                    await HandlePlaceOrderAsync(chatId, telegramId);
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
                    // Check if user is in order placement flow
                    if (_userOrderStates.ContainsKey(telegramId))
                    {
                        await HandleOrderAmountInputAsync(chatId, telegramId, msgText);
                    }
                    else
                    {
                        await ShowMainMenuAsync(chatId);
                    }
                    break;
            }
        }

        private async Task ShowMainMenuAsync(long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { BotTexts.BtnCash, BotTexts.BtnFutures },
                new KeyboardButton[] { BotTexts.BtnPlaceOrder, BotTexts.BtnAccounting },
                new KeyboardButton[] { BotTexts.BtnHelp }
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

        private async Task HandlePlaceOrderAsync(long chatId, long telegramId)
        {
            var (userExists, user) = await _usersApi.GetUserAsync(telegramId);
            if (!userExists || user == null)
            {
                await _botClient.SendTextMessageAsync(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
                return;
            }

            // Get available assets from prices
            var (success, prices) = await _priceApi.GetAllPricesAsync();
            if (!success || prices == null || !prices.Any())
            {
                await _botClient.SendTextMessageAsync(chatId, "در حال حاضر قیمت‌ها در دسترس نیست.");
                return;
            }

            // Show order type selection
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBuy, "order_buy"),
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnSell, "order_sell")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgSelectOrderType, replyMarkup: keyboard);
        }

        private async Task HandleOrderTypeSelectionAsync(long chatId, long telegramId, string orderType)
        {
            var (userExists, user) = await _usersApi.GetUserAsync(telegramId);
            if (!userExists || user == null)
            {
                await _botClient.SendTextMessageAsync(chatId, "کاربر یافت نشد.");
                return;
            }

            // Get available assets from prices
            var (success, prices) = await _priceApi.GetAllPricesAsync();
            if (!success || prices == null || !prices.Any())
            {
                await _botClient.SendTextMessageAsync(chatId, "در حال حاضر قیمت‌ها در دسترس نیست.");
                return;
            }

            // Create order state
            var orderState = new OrderState
            {
                OrderType = orderType,
                UserId = user.Id
            };

            _userOrderStates[telegramId] = orderState;

            // Show available assets
            var assetButtons = new List<InlineKeyboardButton[]>();
            foreach (var price in prices)
            {
                assetButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(price.Asset, $"asset_{price.Asset}")
                });
            }

            assetButtons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
            });

            var keyboard = new InlineKeyboardMarkup(assetButtons.ToArray());

            await _botClient.SendTextMessageAsync(chatId, 
                $"لطفاً نماد مورد نظر برای {orderType} را انتخاب کنید:", 
                replyMarkup: keyboard);
        }

        private async Task HandleAssetSelectionAsync(long chatId, long telegramId, string asset)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendTextMessageAsync(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];
            orderState.Asset = asset;

            // Get current price for the asset
            var (success, prices) = await _priceApi.GetAllPricesAsync();
            if (success && prices != null)
            {
                var price = prices.FirstOrDefault(p => p.Asset == asset);
                if (price != null)
                {
                    orderState.Price = orderState.OrderType == "BUY" ? price.BuyPrice : price.SellPrice;
                }
            }

            // Remove keyboard and ask for amount
            await _botClient.SendTextMessageAsync(chatId, 
                $"{BotTexts.MsgEnterAmount}\nنماد: {asset}\nقیمت: {orderState.Price:N0} تومان",
                replyMarkup: new ReplyKeyboardRemove());
        }

        private async Task HandleOrderAmountInputAsync(long chatId, long telegramId, string amountText)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendTextMessageAsync(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
            {
                await _botClient.SendTextMessageAsync(chatId, "لطفاً مقدار معتبر وارد کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];
            orderState.Amount = amount;

            // Check user's balance for the asset
            var (balanceSuccess, balance, balanceMessage) = await _walletApi.GetWalletBalanceAsync(orderState.UserId, orderState.Asset);
            
            if (orderState.OrderType == "SELL")
            {
                if (!balanceSuccess || balance == null || balance < amount)
                {
                    var availableBalance = balance ?? 0;
                    await _botClient.SendTextMessageAsync(chatId, 
                        string.Format(BotTexts.MsgInsufficientBalance, availableBalance),
                        replyMarkup: new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton[] { BotTexts.BtnBack }
                        })
                        {
                            ResizeKeyboard = true
                        });
                    _userOrderStates.Remove(telegramId);
                    return;
                }
            }

            // Submit the order
            var order = new OrderDto
            {
                Asset = orderState.Asset,
                Amount = orderState.Amount,
                Price = orderState.Price,
                UserId = orderState.UserId,
                Type = orderState.OrderType
            };

            var (orderSuccess, orderMessage) = await _orderApi.SubmitOrderAsync(order);

            if (orderSuccess)
            {
                await _botClient.SendTextMessageAsync(chatId, BotTexts.MsgOrderSuccess,
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { BotTexts.BtnBack }
                    })
                    {
                        ResizeKeyboard = true
                    });
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, 
                    string.Format(BotTexts.MsgOrderFailed, orderMessage),
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { BotTexts.BtnBack }
                    })
                    {
                        ResizeKeyboard = true
                    });
            }

            _userOrderStates.Remove(telegramId);
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
            var telegramId = callbackQuery.From?.Id ?? 0;
            var data = callbackQuery.Data ?? "";

            switch (data)
            {
                case "buy_futures":
                    await _botClient.SendTextMessageAsync(chatId, "بخش خرید آتی در حال توسعه است...");
                    break;

                case "sell_futures":
                    await _botClient.SendTextMessageAsync(chatId, "بخش فروش آتی در حال توسعه است...");
                    break;

                case "order_buy":
                    await HandleOrderTypeSelectionAsync(chatId, telegramId, "BUY");
                    break;

                case "order_sell":
                    await HandleOrderTypeSelectionAsync(chatId, telegramId, "SELL");
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
                    // Clear any order state
                    if (_userOrderStates.ContainsKey(telegramId))
                    {
                        _userOrderStates.Remove(telegramId);
                    }
                    await ShowMainMenuAsync(chatId);
                    break;

                default:
                    // Handle asset selection
                    if (data.StartsWith("asset_"))
                    {
                        var asset = data.Substring(6); // Remove "asset_" prefix
                        await HandleAssetSelectionAsync(chatId, telegramId, asset);
                    }
                    break;
            }

            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }
    }
}