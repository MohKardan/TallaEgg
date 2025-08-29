using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram;
using TallaEgg.TelegramBot.Infrastructure.Handlers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Net.Mime.MediaTypeNames;
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

    public class MarketOrderState
    {
        public string Symbol { get; set; } = "";
        public OrderType OrderType { get; set; } // "Buy" or "Sell"
        public decimal Amount { get; set; }
        public decimal MarketPrice { get; set; }
        public Guid UserId { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public string State { get; set; } = "";
    }

    public class BotHandler : IBotHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly OrderApiClient _orderApi;
        private readonly UsersApiClient _usersApi;
        private readonly AffiliateApiClient _affiliateApi;
        private readonly WalletApiClient _walletApi;

        private readonly Dictionary<long, OrderState> _userOrderStates = new();
        
        private bool _requireReferralCode;
        private string _defaultReferralCode;

        public BotHandler(ITelegramBotClient botClient, OrderApiClient orderApi, UsersApiClient usersApi,
                         AffiliateApiClient affiliateApi, WalletApiClient walletApi,
                         bool requireReferralCode = false, string defaultReferralCode = "ADMIN2024")
        {
            _botClient = botClient;
            _orderApi = orderApi;
            _usersApi = usersApi;
            _affiliateApi = affiliateApi;
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

                if (message.Text == BotBtns.BtnMainMenu)
                    await ShowMainMenuAsync(chatId);
                message.Text = TallaEgg.Core.Utilties.Utils.ConvertPersianDigitsToEnglish(message.Text);



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

                if (user.Status != TallaEgg.Core.Enums.User.UserStatus.Approved)
                {
                    await _botClient.SendMessage(
                         chatId,
                         $"عزیز اکانت کاربری شما فعال نیست {user.FirstName}".AutoRtl()
                     );
                }
                else
                {
                    // Check if user is admin
                    if (await IsUserAdmin(user))
                    {
                        // Check for admin commands first
                        bool isAdminCmd = await HandleAdminCommandsAsync(chatId, telegramId, message, user);
                        if (isAdminCmd) return;
                    }

                    await HandleMainMenuAsync(chatId, telegramId, message, user.Id);
                }
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
                        await _botClient.SendMessage(chatId, BotMsgs.MsgEnterInvite);
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
                var phoneNumber = message.Contact?.PhoneNumber;
                if (phoneNumber.StartsWith("98"))//98938621990
                {
                    phoneNumber = phoneNumber.Replace("98", "0");
                }
                if (phoneNumber.StartsWith("+98"))//98938621990
                {
                    phoneNumber = phoneNumber.Replace("+98", "0");
                }
                var response = await _usersApi.UpdatePhoneAsync(telegramId, phoneNumber);

                if (response.Success)
                {
                    await _botClient.SendMessage(chatId, BotMsgs.MsgPhoneSuccess,
                        replyMarkup: new ReplyKeyboardRemove());
                    await ShowMainMenuAsync(chatId);
                    await _botClient.SendApproveOrRejectUserToAdminsKeyboard(response.Data, Constants.GroupId);
                }
                else
                {
                    await _botClient.SendMessage(chatId, response.Message);
                }
            }
            else
            {
                await _botClient.SendContactKeyboardAsync(chatId);
            }
        }

        private async Task HandleMainMenuAsync(long chatId, long telegramId, Message message, Guid userId)
        {
            var msgText = message.Text ?? "";

            switch (msgText)
            {

                case BotBtns.BtnSpotCreateOrder:
                    await HandleSpotMenuAsync(chatId);
                    break;
                case BotBtns.BtnSpotMarket:
                    await HandleMarketMenuAsync(chatId);
                    break;

                case BotBtns.BtnAccounting:
                    await HandleAccountingMenuAsync(chatId);
                    break;
                case BotBtns.BtnOrderHistory:
                    await ShowTradeHistory(chatId, userId);
                    break;
                case BotBtns.BtnWalletsBalance:
                    await ShowWalletsBalance(chatId, userId);
                    break;

                case BotBtns.BtnHelp:
                    await ShowHelpAsync(chatId);
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
            var user = await _usersApi.GetUserAsync(chatId);

            if (user == null)
            {
                await _botClient.SendMessage(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
                return;
            }

            bool isAdmin = await IsUserAdmin(user);
            isAdmin = true; // for test

            if (isAdmin)
            {
                await _botClient.SendMainKeyboardForAdminAsync(chatId);
            }
            else
            {
                await _botClient.SendMainKeyboardForUserAsync(chatId);
            }
        }
        private async Task HandleSpotMenuAsync(long chatId)
        {
            var user = await _usersApi.GetUserAsync(chatId);

            if (user == null)
            {
                await _botClient.SendMessage(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
                return;
            }
            bool isAdmin = await IsUserAdmin(user);
            isAdmin = true; // for test
            if (!isAdmin)
            {
                await _botClient.SendMessage(chatId, "شما فقط میتوانید با قیمت بازار اقدام به خرید یا فروش نمایید");
                return;
            }
            ''''; ; ; ;
            _userOrderStates.Add(chatId, new OrderState
            {
                UserId = user.Id,
                TradingType = TradingType.Spot
            });

            await _botClient.SendSpotMenuKeyboard(chatId);
        }
        private async Task HandleMarketMenuAsync(long chatId)
        {
            // Show available trading symbols
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("طلای آبشده", $"{InlineCallBackData.market_symbol}_BTC"),
                    InlineKeyboardButton.WithCallbackData("سکه امام", $"{InlineCallBackData.market_symbol}_ETH")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("سکه جدید", $"{InlineCallBackData.market_symbol}_ADA"),
                    InlineKeyboardButton.WithCallbackData("سکه قدیم", $"{InlineCallBackData.market_symbol}_DOT")
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendMessage(chatId, BotMsgs.MsgSelectSymbol, replyMarkup: keyboard);
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
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, "back_to_main")
                }
            });

            await _botClient.SendMessage(chatId, "📈 معاملات آتی\n\nلطفاً نوع معامله خود را انتخاب کنید:", replyMarkup: keyboard);
        }

        private async Task HandleAccountingMenuAsync(long chatId)
        {

            await _botClient.SendAccountingMenuKeyboard(chatId);
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
        private async Task ShowTradeHistory(long chatId, Guid userId)
        {

            var page = await _orderApi.GetUserOrdersAsync(userId, pageNumber: 1, pageSize: 5);
            if (page.Success)
            {
                var text = await OrderListHandler.BuildOrdersListAsync(page.Data!, 1);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.MarkdownV2,
                    replyMarkup: OrderListHandler.BuildPagingKeyboard(page.Data!, 1, userId)
                );
            }
        }

        private async Task ShowWalletsBalance(long chatId, Guid userId)
        {
            var res = await _walletApi.GetUserWalletsBalanceAsync(userId);
            if (res.Success)
            {
                if (res.Data.Any())
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach (var item in res.Data)
                    {
                        stringBuilder.AppendLine($"نوع موجودی : {item.Asset}");
                        stringBuilder.AppendLine($"موجودی قابل برداشت : {item.Balance}");
                        stringBuilder.AppendLine($"موجودی فریز شده : {item.LockedBalance}");
                        stringBuilder.AppendLine($"---------------------------------------- \n");
                    }
                    await _botClient.SendMessage(chatId, stringBuilder.ToString());
                }
                else
                {
                    await _botClient.SendMessage(chatId, "کیف پولی برای شما ثبت نشده است. لطفا برای شارژ حساب با ادمین تماس بگیرید");

                }
            }
            else
            {

                await _botClient.SendMessage(chatId, res.Message);
            }


        }

        private async Task<bool> HandleAdminCommandsAsync(long chatId, long telegramId, Message message, UserDto user)
        {
            var msgText = message.Text ?? "";
            msgText = msgText.ToLower().Trim();
            if (msgText.StartsWith("ش"))
            {
                // ش 09121234567 50000 دلاری
                // ش 09121234567 50000
                var regex = new Regex(@"^ش\s+(?<phone>\d{10,11})\s+(?<amount>\d+)(\s+(?<currency>\S+))?$",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var match = regex.Match(msgText);
                if (!match.Success)
                {
                    await _botClient.SendMessage(message.Chat.Id,
                        "❌ فرمت دستور نادرست است.\nمثال: ش 09121234567 50000 [ریالی/دلاری]");
                }

                var phone = match.Groups["phone"].Value;
                var amount = decimal.Parse(match.Groups["amount"].Value);
                var currency = match.Groups["currency"].Success
                    ? match.Groups["currency"].Value
                    : "ریالی"; // مقدار پیش‌فرض

                string response = $"📌 دستور ثبت شد:\n" +
                                  $"👤 کاربر: {phone}\n" +
                                  $"💰 مبلغ: {amount}\n" +
                                  $"💵 نوع شارژ: {currency}";

                await _botClient.SendMessage(message.Chat.Id, response);
                var userId = await _usersApi.GetUserIdByPhoneNumberAsync(phone);
                if (userId.HasValue)
                {
                    var result = await _walletApi.DepositeAsync(new TallaEgg.Core.Requests.Wallet.DepositRequest
                    {
                        Asset = "rial",
                        Amount = amount,
                        UserId = userId.Value
                    });
                    if (result.Success)
                    {


                        await _botClient.SendMessage(
           message.Chat.Id,
           $"💰 *شارژ کیف‌پول با موفقیت انجام شد.*\n\n" +
           $"💳 دارایی: `ریال`\n" +
           $"💵 مبلغ شارژ: `{amount:N0}` ریال\n" +
           $"🆔 تلفن: `{phone}`\n\n" +
           $"✅ موجودی جدید شما در کیف‌پول به‌روزرسانی شد.", parseMode: ParseMode.Html
       );
                    }
                    else
                    {
                        await _botClient.SendMessage(message.Chat.Id, result.Message);

                    }
                }
                else
                {
                    await _botClient.SendMessage(message.Chat.Id, "شماره تلفن معتبر نیست");

                }

                return true;

            }

            if (msgText.StartsWith("ک"))
            {
                var msgSplit = msgText.Split(" ");
                string? q = null;
                if (msgSplit.Length > 1) q = msgSplit[1];
                var page = await _usersApi.GetUsersAsync(pageNumber: 1, pageSize: 5, q);
                if (page.Success)
                {
                    var text = await UserListHandler.BuildUsersListAsync(page.Data!, 1, q);

                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: text,
                        parseMode: ParseMode.MarkdownV2,
                        replyMarkup: UserListHandler.BuildPagingKeyboard(page.Data!, 1, q)
                    );
                }
                else await _botClient.SendMessage(chatId, page.Message);
                return true;
            }
            if (msgText.StartsWith("م "))
            {
                var msgSplit = msgText.Split(" ");
                string phone = "";
                if (msgSplit.Length > 1) phone = msgSplit[1];
                var useId = await _usersApi.GetUserIdByPhoneNumberAsync(phone);
                if (useId.HasValue)
                {
                    await ShowWalletsBalance(chatId, useId.Value);
                }
                else
                {
                    await _botClient.SendMessage(chatId, "شماره تلفن پیدا نشد");
                }
                return true;
            }
            if (msgText.StartsWith("س "))
            {
                var msgSplit = msgText.Split(" ");
                string phone = "";
                if (msgSplit.Length > 1) phone = msgSplit[1];
                var useId = await _usersApi.GetUserIdByPhoneNumberAsync(phone);
                if (useId.HasValue)
                {
                    await ShowTradeHistory(chatId, useId.Value);
                }
                else
                {
                    await _botClient.SendMessage(chatId, "شماره تلفن پیدا نشد");
                }
                return true;
            }
            return false;

            //switch (msgText.ToLower())
            //{
            //    case "/admin_referral_on":
            //        _requireReferralCode = true;
            //        await _botClient.SendMessage(chatId,
            //            "✅ اجباری بودن کد دعوت فعال شد.\n" +
            //            "کاربران جدید باید کد دعوت داشته باشند.");
            //        return true;

            //    case "/admin_referral_off":
            //        _requireReferralCode = false;
            //        await _botClient.SendMessage(chatId,
            //            "❌ اجباری بودن کد دعوت غیرفعال شد.\n" +
            //            $"کاربران جدید با کد پیش‌فرض '{_defaultReferralCode}' ثبت‌نام خواهند شد.");
            //        return true;

            //    case "/admin_referral_status":
            //        var status = _requireReferralCode ? "فعال" : "غیرفعال";
            //        await _botClient.SendMessage(chatId,
            //            $"📊 وضعیت فعلی:\n" +
            //            $"اجباری بودن کد دعوت: {status}\n" +
            //            $"کد پیش‌فرض: {_defaultReferralCode}\n\n" +
            //            $"دستورات مدیریتی:\n" +
            //            $"/admin_referral_on - فعال کردن اجباری بودن کد دعوت\n" +
            //            $"/admin_referral_off - غیرفعال کردن اجباری بودن کد دعوت\n" +
            //            $"/admin_referral_status - نمایش وضعیت فعلی");
            //        return true;

            //    default:
            //        return false; // Not an admin command, continue with normal processing
            //}
        }

        private async Task<bool> IsUserAdmin(UserDto user)
        {
            var ids = await _botClient.GetAdminUserIdsAsync(Constants.GroupId);
            return ids.Contains(user.TelegramId);
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
            bool isAdmin = await IsUserAdmin(user);
            isAdmin = true; // for test
            if (!isAdmin)
            {
                await _botClient.SendMessage(chatId, "شما فقط میتوانید با قیمت بازار اقدام به خرید یا فروش نمایید");

            }
            else
            {
                // Show order type selection
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnSpotMarketBuy, InlineCallBackData.order_buy),
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnSpotMarketSell, InlineCallBackData.order_sell)
                    //TODO ننیاز به بررسی بیشتر
                    // چرا دکمه ها تغییر کرده است
                },
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, InlineCallBackData.back_to_main)
                }
            });

                await _botClient.SendMessage(chatId, BotMsgs.MsgSelectOrderType, replyMarkup: keyboard);
            }

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

            // Get available assets 
            //TODO اینحا باید نمادهای معاملاتیرو از یجایی بحونیم
            // فعلا نمادهای معاملاتی به صورت HardCode
            // Mesqal Au Abshode
            var assets = new[] { "MAUA/IRR", "XAU/IRR", "BTC/USDT", "ETH/USDT", "XAU/USD", "XAG/USD" };

            // Show available assets
            var assetButtons = new List<InlineKeyboardButton[]>();

            foreach (var asset in assets)
            {
                assetButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(asset, $"asset_{asset}")
                });
            }

            assetButtons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, "back_to_main")
            });

            var keyboard = new InlineKeyboardMarkup(assetButtons.ToArray());

            await _botClient.SendMessage(chatId, BotMsgs.MsgSelectAsset, replyMarkup: keyboard);
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
                $"{BotMsgs.MsgEnterAmount}",
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
                    var backBtn = new KeyboardButton(BotBtns.BtnBack);
                    await _botClient.SendMessage(chatId,
                        string.Format(BotMsgs.MsgInsufficientBalance, availableBalance),
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
            var confirmationMessage = string.Format(BotMsgs.MsgOrderConfirmation,
                orderState.Asset,
                orderState.OrderType,
                orderState.Amount,
                orderState.Price,
                totalValue);

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnConfirm, InlineCallBackData.confirm_order),
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnCancel, InlineCallBackData.cancel_order)
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

                var backBtn = new KeyboardButton(BotBtns.BtnBack);
                if (orderSuccess)
                {
                    await _botClient.SendMessage(chatId, BotMsgs.MsgOrderSuccess,
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
                        string.Format(BotMsgs.MsgOrderFailed, orderMessage),
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

        public async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {

            var chatId = callbackQuery.Message?.Chat.Id ?? 0;
            var telegramId = callbackQuery.From?.Id ?? 0;
            var message = callbackQuery.Message;
            var data = callbackQuery.Data ?? "";


            switch (data)
            {
                case InlineCallBackData.buy_spot:
                case InlineCallBackData.sell_spot:
                    //await ShowSpotSymbolOptionsAsync(chatId);

                    OrderType orderType = data == InlineCallBackData.buy_spot ? OrderType.Buy : OrderType.Sell;

                    await HandleOrderTypeSelectionAsync(chatId, telegramId, orderType);

                    break;

                //case InlineCallBackData.order_buy:
                //    await HandleOrderTypeSelectionAsync(chatId, telegramId, OrderType.Buy);
                //    break;

                //case InlineCallBackData.order_sell:
                //    await HandleOrderTypeSelectionAsync(chatId, telegramId, OrderType.Sell);
                //    break;

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

                //case "take_order_spot":
                case InlineCallBackData.market_spot:
                    await HandleMarketMenuAsync(chatId);
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
                    else if (data.StartsWith("approve_"))
                    {
                        var telegramUserId = data["approve_".Length..];

                        await ApproveUser(long.Parse(telegramUserId), telegramId, message);

                    }
                    else if (data.StartsWith("reject_"))
                    {
                        var telegramUserId = data["reject_".Length..];

                        await RejectUser(long.Parse(telegramUserId), telegramId, message);

                    }
                    else if (data.StartsWith("orders_"))
                    {

                        var parts = data.Split('_'); // orders_{userId}_{page}
                        if (parts.Length == 3 &&
                            Guid.TryParse(parts[1], out var uid) &&
                            int.TryParse(parts[2], out var pageNum))
                        {
                            var page = await _orderApi.GetUserOrdersAsync(uid, pageNum, pageSize: 5);

                            var text = await OrderListHandler.BuildOrdersListAsync(page.Data!, pageNum);

                            // ویرایش پیام قبلی
                            await _botClient.EditMessageText(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: text,
                                parseMode: ParseMode.MarkdownV2,
                                replyMarkup: OrderListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // بستن "در حال فکر کردن..." روی دکمه
                            await _botClient.AnswerCallbackQuery(callbackQuery.Id);
                        }
                    }

                    else if (data != null && data.StartsWith("users_"))
                    {
                        var parts = data.Split('_', 3); // users_{page}_{query}
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int newPage))
                        {
                            string? query = parts.Length == 3 ? parts[2] : null;

                            // دیتای کاربران رو برای صفحه جدید بخون
                            var page = await _usersApi.GetUsersAsync(newPage, 5, query); // (pageNumber, pageSize, query)

                            var text = await UserListHandler.BuildUsersListAsync(page.Data!, newPage, query);

                            // ویرایش پیام قبلی
                            await _botClient.EditMessageText(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: text,
                                parseMode: ParseMode.MarkdownV2,
                                replyMarkup: UserListHandler.BuildPagingKeyboard(page.Data!, newPage, query)
                            );

                            // بستن "در حال فکر کردن..." روی دکمه
                            await _botClient.AnswerCallbackQuery(callbackQuery.Id);
                        }
                    }
                    break;
            }

            await _botClient.AnswerCallbackQuery(callbackQuery.Id);
        }

        private async Task ApproveUser(long telegramUserId, long adminTgId, Message originalMsg)
        {
            await _usersApi.UpdateUserStatusAsync(telegramUserId, TallaEgg.Core.Enums.User.UserStatus.Approved);

            // ویرایش پیام ادمین
            await _botClient.EditMessageText(
                chatId: originalMsg.Chat.Id,
                messageId: originalMsg.MessageId,
                text: $"{originalMsg.Text}\n\n✅ توسط ادمین {adminTgId} تأیید شد.",
                replyMarkup: null);

            // اطلاع‌رسانی به کاربر
            await _botClient.SendMessage(telegramUserId, "درخواست شما تأیید شد\n حالا میتوانید از خدمات ما استفاده کنید.");
        }

        private async Task RejectUser(long telegramUserId, long adminTgId, Message originalMsg)
        {
            await _usersApi.UpdateUserStatusAsync(telegramUserId, TallaEgg.Core.Enums.User.UserStatus.Rejected);

            await _botClient.EditMessageText(
                chatId: originalMsg.Chat.Id,
                messageId: originalMsg.MessageId,
                text: $"{originalMsg.Text}\n\n❌ توسط ادمین {adminTgId} رد شد.",
                replyMarkup: null);

            // اطلاع‌رسانی به کاربر
            await _botClient.SendMessage(telegramUserId, "درخواست شما رد شد.");
        }

        public Task HandleMessageAsync(object message)
        {
            throw new NotImplementedException();
        }

    }
}