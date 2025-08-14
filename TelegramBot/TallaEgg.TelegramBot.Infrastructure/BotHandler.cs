using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Keyboards.ReplyKeyboards;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static TallaEgg.TelegramBot.Infrastructure.Clients.OrderApiClient;

namespace TallaEgg.TelegramBot
{
    public class OrderState
    {
        public TradingType TradingType { get; set; } // "Spot" or "Futures"
        public OrderType OrderType { get; set; } // "Buy" or "Sell"
        public string Asset { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public Guid UserId { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public string? Notes { get; set; } = null;
        public string State { get; internal set; } = "";
    }

    public class BotHandler : IBotHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly OrderApiClient _orderApi;
        private readonly UsersApiClient _usersApi;
        private readonly AffiliateApiClient _affiliateApi;
        private readonly PriceApiClient _priceApi;
        private readonly WalletApiClient _walletApi;
        private readonly Dictionary<long, OrderState> _userOrderStates = new();
        private bool _requireReferralCode;
        private string _defaultReferralCode;

        public BotHandler(ITelegramBotClient botClient, OrderApiClient orderApi, UsersApiClient usersApi,
                         AffiliateApiClient affiliateApi, PriceApiClient priceApi, WalletApiClient walletApi,
                         bool requireReferralCode = false, string defaultReferralCode = "ADMIN2024")
        {
            _botClient = botClient;
            _orderApi = orderApi;
            _usersApi = usersApi;
            _affiliateApi = affiliateApi;
            _priceApi = priceApi;
            _walletApi = walletApi;
            _requireReferralCode = requireReferralCode;
            _defaultReferralCode = defaultReferralCode;

            // Cleanup old states every hour
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    try
                    {
                        var expiredKeys = _userOrderStates.Keys
                            .Where(k => _userOrderStates[k].IsConfirmed)
                            .ToList();
                        foreach (var key in expiredKeys)
                            _userOrderStates.Remove(key);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in cleanup: {ex.Message}");
                    }
                }
            });
        }

        public async Task HandleUpdateAsync(object updateObj)
        {
            try
            {
                var update = (Update)updateObj;

                if (update.Type == UpdateType.CallbackQuery)
                {
                    await HandleCallbackQueryAsync(update.CallbackQuery);
                    return;
                }

                if (update.Message is not { } message)
                    return;

                if (message.Type != MessageType.Contact && message.Type != MessageType.Text)
                    return;


                var chatId = message.Chat.Id;
                var telegramId = message.From?.Id ?? 0;

                if (message.Text == BotTexts.MainMenu) await _botClient.SendMainKeyboardAsync(chatId);
                message.Text = Utils.ConvertPersianDigitsToEnglish(message.Text);
                // Check if user exists
                var user = await _usersApi.GetUserAsync(telegramId);

                if (user == null)
                {
                    await HandleNewUserAsync(chatId, telegramId, message);
                    return;
                }

                if (string.IsNullOrEmpty(user?.PhoneNumber))
                {
                    await HandlePhoneNumberRequestAsync(chatId, telegramId, message);
                    return;
                }

                // Check for admin commands first
                if (await HandleAdminCommandsAsync(chatId, telegramId, message, user))
                {
                    return;
                }

                await HandleMainMenuAsync(chatId, telegramId, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in HandleUpdateAsync: {ex.Message}");

            }

        }

        private async Task HandleNewUserAsync(long chatId, long telegramId, Message message)
        {
            var msgText = message.Text ?? "";

            if (msgText.StartsWith("/start"))
            {
                var parts = msgText.Split('?', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    var invitationCode = parts[1];
                    await HandleInvitationCodeAsync(chatId, telegramId, invitationCode, message);
                }
                else
                {
                    // Check if referral code is required
                    if (_requireReferralCode)
                    {
                        await _botClient.SendMessage(chatId, BotTexts.MsgEnterInvite);
                    }
                    else
                    {
                        // Use default referral code and register user directly
                        await HandleInvitationCodeAsync(chatId, telegramId, _defaultReferralCode, message);
                    }
                }



            }
        }

        private async Task HandleInvitationCodeAsync(long chatId, long telegramId, string invitationCode, Message message)
        {
            // First register the user
            var (regSuccess, regMessage, userId) = await _usersApi.RegisterUserAsync(telegramId, invitationCode, message.From?.Username, message.From?.FirstName, message.From?.LastName);

            if (regSuccess && userId.HasValue)
            {
                // Then use the invitation
                //   var (useSuccess, useMessage, invitationId) = await _affiliateApi.UseInvitationAsync(invitationCode, userId.Value);

                //if (useSuccess)
                //{
                await _botClient.SendContactKeyboardAsync(chatId);

                //else
                //{
                //    await _botClient.SendMessage(chatId, $"خطا در استفاده از کد دعوت: {useMessage}");
                //}
            }
            else
            {
                await _botClient.SendMessage(chatId, $"خطا در ثبت‌نام: {regMessage}");
            }
        }

        private async Task HandlePhoneNumberRequestAsync(long chatId, long telegramId, Message message)
        {
            if (message.Contact?.PhoneNumber != null)
            {
                var (success, updateMessage) = await _usersApi.UpdatePhoneAsync(telegramId, message.Contact.PhoneNumber);

                if (success)
                {
                    await _botClient.SendMessage(chatId, BotTexts.MsgPhoneSuccess,
                        replyMarkup: new ReplyKeyboardRemove());
                    await ShowMainMenuAsync(chatId);
                }
                else
                {
                    await _botClient.SendMessage(chatId, $"خطا در ثبت شماره تلفن: {updateMessage}");
                }
            }
            else
            {
                await _botClient.SendContactKeyboardAsync(chatId);
            }
        }

        private async Task HandleMainMenuAsync(long chatId, long telegramId, Message message)
        {
            var msgText = message.Text ?? "";

            switch (msgText)
            {

                case BotTexts.BtnSpot:
                    await HandleSpotMenuAsync(chatId);
                    break;

                case BotTexts.BtnFutures:
                    await HandleFuturesMenuAsync(chatId);
                    break;

                case BotTexts.BtnAccounting:
                    await _botClient.SendMessage(chatId, "📊 بخش حسابداری در حال توسعه است...");
                    break;

                case BotTexts.BtnHelp:
                    await ShowHelpAsync(chatId);
                    break;

                case BotTexts.BtnMakeOrderSpot:
                    //await HandleMakeOrderSpotMenuAsync(chatId);
                    await ShowSpotOrderTypeSelectionAsync(chatId);
                    break;

                default:
                    // Check if user is in order flow
                    if (_userOrderStates.ContainsKey(telegramId))
                    {
                        var orderState = _userOrderStates[telegramId];
                        if (!orderState.IsConfirmed && orderState.State == "waiting_for_amount")
                        {
                            await HandleOrderAmountInputAsync(chatId, telegramId, msgText);
                            return;
                        }
                        if (!orderState.IsConfirmed && orderState.State == "waiting_for_price")
                        {
                            await HandleOrderPriceInputAsync(chatId, telegramId, msgText);
                            return;
                        }
                    }

                    await ShowMainMenuAsync(chatId);
                    break;
            }
        }

        private async Task ShowMainMenuAsync(long chatId)
        {
            await _botClient.SendMainKeyboardAsync(chatId);
        }
        /// <summary>
        /// Place Order همان مفهوم Make Order را دارد و به معنای ثبت سفارش است
        /// </summary>
        /// <param name="chatId"></param>
        /// <param name="telegramId"></param>
        /// <returns></returns>
        private async Task HandlePlaceOrderAsync(long chatId, long telegramId)
        {
            //var (userExists, user) = await _usersApi.GetUserAsync(telegramId);
            //if (!userExists || user == null)
            //{
            //    await _botClient.SendMessage(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
            //    return;
            //}

            // Show trading type selection
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnSpot, "trading_spot"),
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnFutures, "trading_futures")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendMessage(chatId, BotTexts.MsgSelectTradingType, replyMarkup: keyboard);
        }
        private async Task HandleSpotMenuAsync(long chatId)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnMakeOrderSpot, "trading_spot"),
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnTakeOrder, "take_order_spot")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendMessage(chatId, "🎯 منوی معاملات نقدی\nلطفاً یکی از گزینه‌های زیر را انتخاب کنید:", replyMarkup: keyboard);
        }

        private async Task ShowSpotOrderTypeSelectionAsync(long chatId)
        {
            // Note: This method should be called with telegramId, not chatId
            // The state should be managed with telegramId as key
            await _botClient.SendMessage(chatId, "این گزینه در حال توسعه است. لطفاً از منوی اصلی استفاده کنید.");
            await ShowMainMenuAsync(chatId);
        }
        private async Task HandleMakeOrderSpotMenuAsync(long chatId)
        {
            // Note: This method should be called with telegramId, not chatId
            // The state should be managed with telegramId as key
            await _botClient.SendMessage(chatId, "این گزینه در حال توسعه است. لطفاً از منوی اصلی استفاده کنید.");
            await ShowMainMenuAsync(chatId);
        }
        private async Task ShowSpotSymbolOptionsAsync(long chatId)
        {
            // Note: This method should be called with telegramId, not chatId
            // The state should be managed with telegramId as key
            await _botClient.SendMessage(chatId, "این گزینه در حال توسعه است. لطفاً از منوی اصلی استفاده کنید.");
            await ShowMainMenuAsync(chatId);
        }

        private async Task HandleFuturesMenuAsync(long chatId)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("🛒 خرید آتی", InlineCallBackData.buy_futures),
                    InlineKeyboardButton.WithCallbackData("🛍️ فروش آتی", InlineCallBackData.sell_futures)
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendMessage(chatId, "📈 معاملات آتی\n\nلطفاً نوع معامله خود را انتخاب کنید:", replyMarkup: keyboard);
        }



        private async Task ShowHelpAsync(long chatId)
        {
            var helpText = "❓ راهنما\n\n" +
                          "💰 نقدی: معاملات نقدی و فوری\n" +
                          "📈 آتی: معاملات آتی و قراردادهای آتی\n" +
                          "📊 حسابداری: مشاهده موجودی و تاریخچه معاملات\n" +
                          "❓ راهنما: این صفحه\n\n" +
                          "برای پشتیبانی با تیم فنی تماس بگیرید.";

            await _botClient.SendMessage(chatId, helpText);
        }

        private async Task<bool> HandleAdminCommandsAsync(long chatId, long telegramId, Message message, UserDto user)
        {
            var msgText = message.Text ?? "";

            // Check if user is admin
            if ((!IsUserAdmin(user)))
            {
                return false; // Not an admin, continue with normal processing
            }

            switch (msgText.ToLower())
            {
                case "/admin_referral_on":
                    _requireReferralCode = true;
                    await _botClient.SendMessage(chatId,
                        "✅ اجباری بودن کد دعوت فعال شد.\n" +
                        "کاربران جدید باید کد دعوت داشته باشند.");
                    return true;

                case "/admin_referral_off":
                    _requireReferralCode = false;
                    await _botClient.SendMessage(chatId,
                        "❌ اجباری بودن کد دعوت غیرفعال شد.\n" +
                        $"کاربران جدید با کد پیش‌فرض '{_defaultReferralCode}' ثبت‌نام خواهند شد.");
                    return true;

                case "/admin_referral_status":
                    var status = _requireReferralCode ? "فعال" : "غیرفعال";
                    await _botClient.SendMessage(chatId,
                        $"📊 وضعیت فعلی:\n" +
                        $"اجباری بودن کد دعوت: {status}\n" +
                        $"کد پیش‌فرض: {_defaultReferralCode}\n\n" +
                        $"دستورات مدیریتی:\n" +
                        $"/admin_referral_on - فعال کردن اجباری بودن کد دعوت\n" +
                        $"/admin_referral_off - غیرفعال کردن اجباری بودن کد دعوت\n" +
                        $"/admin_referral_status - نمایش وضعیت فعلی");
                    return true;

                default:
                    return false; // Not an admin command, continue with normal processing
            }
        }

        private bool IsUserAdmin(UserDto user)
        {
            //  Check if user has admin status or is a known admin Telegram ID
            // var adminTelegramIds = new[] { 123456789L }; // Add actual admin Telegram IDs here
            //return user.Status?.ToLower().Contains("admin") == true ||
            //       user.Status?.ToLower().Contains("root") == true ||
            //       adminTelegramIds.Contains(user.TelegramId);

            return false;
        }

        private async Task HandleTradingTypeSelectionAsync(long chatId, long telegramId, TradingType tradingType)
        {
            var user = await _usersApi.GetUserAsync(telegramId);
            
            if (user == null)
            {
                await _botClient.SendMessage(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
                return;
            }

            // Create order state
            var orderState = new OrderState
            {
                TradingType = tradingType,
                UserId = user.Id
            };

            _userOrderStates[telegramId] = orderState;

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

            await _botClient.SendMessage(chatId, BotTexts.MsgSelectOrderType, replyMarkup: keyboard);
        }

        private async Task HandleOrderTypeSelectionAsync(long chatId, long telegramId, OrderType orderType)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];
            orderState.OrderType = orderType;

            // Get available assets from prices
            var (success, prices) = await _priceApi.GetAllPricesAsync();
            if (!success || prices == null || !prices.Any())
            {
                await _botClient.SendMessage(chatId, "در حال حاضر قیمت‌ها در دسترس نیست.");
                return;
            }

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

            await _botClient.SendMessage(chatId, BotTexts.MsgSelectAsset, replyMarkup: keyboard);
        }

        private async Task HandleAssetSelectionAsync(long chatId, long telegramId, string asset)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];
            orderState.Asset = asset;
            orderState.State = "waiting_for_amount";

            //// Get current price for the asset
            //var (success, prices) = await _priceApi.GetAllPricesAsync();
            //if (success && prices != null)
            //{
            //    var price = prices.FirstOrDefault(p => p.Asset == asset);
            //    if (price != null)
            //    {
            //        orderState.Price = orderState.OrderType == "Buy" ? price.BuyPrice : price.SellPrice;
            //    }
            //}

            // Remove keyboard and ask for amount
            await _botClient.SendMessage(chatId,
                //$"{BotTexts.MsgEnterAmount}\nنماد: {asset}\nقیمت: {orderState.Price:N0} تومان",
                $"{BotTexts.MsgEnterAmount}",
                replyMarkup: new ReplyKeyboardRemove());
        }

        private async Task HandleOrderAmountInputAsync(long chatId, long telegramId, string amountText)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
            {
                await _botClient.SendMessage(chatId, "لطفاً مقدار معتبر وارد کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];
            orderState.Amount = amount;
            orderState.State = "waiting_for_price";


            await _botClient.SendMessage(chatId,
             //$"{BotTexts.MsgEnterAmount}\nنماد: {asset}\nقیمت: {orderState.Price:N0} تومان",
             $"لطفا قیمت رو وارد کنید",
             replyMarkup: new ReplyKeyboardRemove());


        }

        private async Task HandleOrderPriceInputAsync(long chatId, long telegramId, string amountText)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            if (!decimal.TryParse(amountText, out var price) || price <= 0)
            {
                await _botClient.SendMessage(chatId, "لطفاً قیمت معتبر وارد کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];
            orderState.Price = price;
            orderState.State = "";


            // Check user's balance for the asset (for SELL orders)
            if (orderState.OrderType == OrderType.Sell)
            {
                var (balanceSuccess, balance, balanceMessage) = await _walletApi.GetWalletBalanceAsync(orderState.UserId, orderState.Asset);

                if (!balanceSuccess || balance == null || balance < orderState.Amount)
                {
                    var availableBalance = balance ?? 0;
                    var backBtn = new KeyboardButton(BotTexts.BtnBack);
                    await _botClient.SendMessage(chatId,
                        string.Format(BotTexts.MsgInsufficientBalance, availableBalance),
                        replyMarkup: new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton[] { backBtn }
                        })
                        {
                            ResizeKeyboard = true
                        });
                    _userOrderStates.Remove(telegramId);
                    return;
                }
            }


            var totalValue = orderState.Amount * orderState.Price;
            var confirmationMessage = string.Format(BotTexts.MsgOrderConfirmation,
                orderState.Asset,
                orderState.OrderType,
                orderState.Amount,
                orderState.Price,
                totalValue);

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnConfirm, "confirm_order"),
                    InlineKeyboardButton.WithCallbackData(BotTexts.BtnCancel, "cancel_order")
                }
            });

            //orderState.IsConfirmed = true;
            await _botClient.SendMessage(chatId, confirmationMessage, replyMarkup: keyboard);
        }

        private async Task HandleOrderConfirmationAsync(long chatId, long telegramId)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];

            // Submit the order using the new Maker/Taker system
            var order = new OrderDto
            {
                Asset = orderState.Asset,
                Amount = orderState.Amount,
                Price = orderState.Price,
                UserId = orderState.UserId,
                Type = orderState.OrderType,
                TradingType = orderState.TradingType
            };

            try
            {
                var (orderSuccess, orderMessage) = await _orderApi.SubmitOrderAsync(order);

                var backBtn = new KeyboardButton(BotTexts.BtnBack);
                if (orderSuccess)
                {
                    await _botClient.SendMessage(chatId, BotTexts.MsgOrderSuccess,
                        replyMarkup: new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton[] { backBtn }
                        })
                        {
                            ResizeKeyboard = true
                        });
                }
                else
                {
                    await _botClient.SendMessage(chatId,
                        string.Format(BotTexts.MsgOrderFailed, orderMessage),
                        replyMarkup: new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton[] { backBtn }
                        })
                        {
                            ResizeKeyboard = true
                        });
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendMessage(chatId, $"خطا در ثبت سفارش: {ex.Message}");
            }
            finally
            {
                _userOrderStates.Remove(telegramId);
            }
        }

        private async Task HandleChargeWalletAsync(long chatId, long telegramId)
        {
            //var (userExists, user) = await _usersApi.GetUserAsync(telegramId);
            //if (!userExists || user == null)
            //{
            //    await _botClient.SendMessage(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
            //    return;
            //}

            //var keyboard = new InlineKeyboardMarkup(new[]
            //{
            //    new InlineKeyboardButton[]
            //    {
            //        InlineKeyboardButton.WithCallbackData("💳 کارت بانکی", "charge_card"),
            //        InlineKeyboardButton.WithCallbackData("🏦 بانک", "charge_bank")
            //    },
            //    new InlineKeyboardButton[]
            //    {
            //        InlineKeyboardButton.WithCallbackData(BotTexts.BtnBack, "back_to_main")
            //    }
            //});

            //await _botClient.SendMessage(chatId,
            //    "💳 شارژ کیف پول\n\n" +
            //    "لطفاً روش پرداخت خود را انتخاب کنید:\n\n" +
            //    "💳 کارت بانکی: شارژ از طریق کارت بانکی\n" +
            //    "🏦 بانک: واریز به حساب بانکی",
            //    replyMarkup: keyboard);
        }

        public async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message?.Chat.Id ?? 0;
            var telegramId = callbackQuery.From?.Id ?? 0;
            var data = callbackQuery.Data ?? "";

            switch (data)
            {
                case InlineCallBackData.buy_spot:
                case InlineCallBackData.sell_spot:
                    await ShowSpotSymbolOptionsAsync(chatId);
                    break;
                case InlineCallBackData.buy_futures:
                    await _botClient.SendMessage(chatId, "بخش خرید آتی در حال توسعه است...");
                    break;

                case InlineCallBackData.sell_futures:
                    await _botClient.SendMessage(chatId, "بخش فروش آتی در حال توسعه است...");
                    break;

                case InlineCallBackData.trading_spot:
                    await HandleTradingTypeSelectionAsync(chatId, telegramId, TradingType.Spot);
                    break;

                case InlineCallBackData.trading_futures:
                    await HandleTradingTypeSelectionAsync(chatId, telegramId, TradingType.Futures);
                    break;

                case InlineCallBackData.order_buy:
                    await HandleOrderTypeSelectionAsync(chatId, telegramId, OrderType.Buy);
                    break;

                case InlineCallBackData.order_sell:
                    await HandleOrderTypeSelectionAsync(chatId, telegramId, OrderType.Sell);
                    break;

                case InlineCallBackData.confirm_order:
                    await HandleOrderConfirmationAsync(chatId, telegramId);
                    break;

                case InlineCallBackData.cancel_order:
                    if (_userOrderStates.ContainsKey(telegramId))
                    {
                        _userOrderStates.Remove(telegramId);
                    }
                    await ShowMainMenuAsync(chatId);
                    break;

                case "take_order_spot":
                    await _botClient.SendMessage(chatId, "بخش بازار در حال توسعه است...");
                    break;

                case InlineCallBackData.charge_card:
                    await _botClient.SendMessage(chatId,
                        "💳 شارژ از طریق کارت بانکی\n\n" +
                        "لطفاً مبلغ مورد نظر را وارد کنید (به تومان):\n" +
                        "مثال: 100000");
                    break;

                case InlineCallBackData.charge_bank:
                    await _botClient.SendMessage(chatId,
                        "🏦 واریز به حساب بانکی\n\n" +
                        "شماره حساب: 1234567890\n" +
                        "شماره کارت: 1234-5678-9012-3456\n" +
                        "به نام: شرکت تالا\n\n" +
                        "پس از واریز، رسید را برای ما ارسال کنید.");
                    break;

                case InlineCallBackData.back_to_main:
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

            await _botClient.AnswerCallbackQuery(callbackQuery.Id);
        }

        public Task HandleMessageAsync(object message)
        {
            throw new NotImplementedException();
        }

    }
}