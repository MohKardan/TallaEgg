using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.Infrastructure;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram;
using TallaEgg.TelegramBot.Infrastructure.Handlers;
using Telegram.Bot;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Net.Mime.MediaTypeNames;
using static TallaEgg.TelegramBot.Infrastructure.Clients.OrderApiClient;

namespace TallaEgg.TelegramBot
{
    public class OrderState
    {
        public OrderType OrderType { get; set; } // "Limit" or "Market"
        public TradingType TradingType { get; set; } // "Spot" or "Futures"
        public OrderSide OrderSide { get; set; } // "Buy" or "Sell"
        public string Asset { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public decimal? BestBidPrice { get; set; }
        public decimal? BestAskPrice { get; set; }
        public Guid UserId { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public string? Notes { get; set; } = null;
        public string State { get; internal set; } = "";
    }

    public partial class BotHandler : IBotHandler
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

        public async Task HandleMessageAsync(Message message)
        {
            try
            {
                var chatId = message.Chat.Id;
                var telegramId = message.From?.Id ?? 0;

                message.Text = TallaEgg.Core.Utilties.Utils.ConvertPersianDigitsToEnglish(message.Text);

                // Check if user exists
                var user = await _usersApi.GetUserAsync(telegramId);

                if (user == null)
                {
                    await _botClient.SendMessage(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
                    await HandleNewUserAsync(chatId, telegramId, message);
                    return;
                }

                _userOrderStates.TryAdd(chatId, new OrderState
                {
                    UserId = user.Id
                });

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
                    // احتمالا بهتره که در آینده این کار را رول حسابدار انجام دهد
                    // Check if user is admin
                    //if (await IsTelegramAdmin(user))
                    if (user.Role == TallaEgg.Core.Enums.User.UserRole.Admin)
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
                case BotBtns.BtnMainMenu:
                    await ShowMainMenuAsync(chatId);
                    break;

                case BotBtns.BtnSpotSubmitPrice:
                case BotBtns.BtnSpotCreateOrder:
                case BotBtns.BtnSpotMarket:

                    OrderType orderType = (msgText == BotBtns.BtnSpotCreateOrder ||
                                           msgText == BotBtns.BtnSpotSubmitPrice) ?
                                           OrderType.Limit : OrderType.Market;

                    _userOrderStates[telegramId].OrderType = orderType;

                    await ShowSymbolsAsync(chatId, telegramId);

                    break;

                case BotBtns.BtnAccounting:
                    await HandleAccountingMenuAsync(chatId);
                    break;
                case BotBtns.BtnOrderHistory:
                    await ShowOrderHistory(chatId, userId);
                    break;
                case BotBtns.BtnTradeHistory:
                    await ShowTradeHistory(chatId, userId);
                    break;
                case BotBtns.BtnActiveOrders:
                    await ShowActiveOrders(chatId, userId);
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

                    OrderSide orderSide = data == InlineCallBackData.buy_spot ? OrderSide.Buy : OrderSide.Sell;
                    _userOrderStates[telegramId].OrderSide = orderSide;

                    _userOrderStates[telegramId].State = "waiting_for_amount";
                    await _botClient.SendMessage(chatId,
                                                 $"لطفا مقدار را وارد کنید",
                                                 replyMarkup: new ReplyKeyboardRemove());

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

                        if (!_userOrderStates.ContainsKey(telegramId))
                        {
                            await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                            return;
                        }

                        _userOrderStates[telegramId].Asset = asset;

                        _userOrderStates[telegramId].State = "waiting_for_select_side";

                        TallaEgg.Core.DTOs.ApiResponse<BestPricesDto> apiResponse = await _orderApi.GetBestPricesAsync(asset);
                        if (apiResponse != null && apiResponse.Success)
                        {
                            apiResponse.Data.BestBidPrice *= 4.3318m;
                            apiResponse.Data.BestAskPrice *= 4.3318m;

                            await _botClient.SendMessage(chatId,
                                            $"📊 <b>بهترین قیمت‌های بازار</b>\n\n" +
                                            $"💰 <b>خرید:</b> <code>{apiResponse.Data.BestBidPrice:N0}</code>\n" +
                                            $"💸 <b>فروش:</b> <code>{apiResponse.Data.BestAskPrice:N0}</code>",
                                            Telegram.Bot.Types.Enums.ParseMode.Html);

                            _userOrderStates[telegramId].BestBidPrice = apiResponse.Data.BestBidPrice;
                            _userOrderStates[telegramId].BestAskPrice = apiResponse.Data.BestAskPrice;
                        }

                        await _botClient.SendSpotSideMenuKeyboard(chatId);

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
                    else if (data.StartsWith("trades_"))
                    {
                        var parts = data.Split('_'); // trades_{userId}_{page}
                        if (parts.Length == 3 &&
                            Guid.TryParse(parts[1], out var uid) &&
                            int.TryParse(parts[2], out var pageNum))
                        {
                            var page = await _orderApi.GetUserTradesAsync(uid, pageNum, pageSize: 5);

                            var text = await TradeListHandler.BuildTradesListAsync(page.Data!, pageNum);

                            // ویرایش پیام قبلی
                            await _botClient.EditMessageText(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: text,
                                parseMode: ParseMode.MarkdownV2,
                                replyMarkup: TradeListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // بستن "در حال فکر کردن..." روی دکمه
                            await _botClient.AnswerCallbackQuery(callbackQuery.Id);
                        }
                    }
                    else if (data.StartsWith("cancel_order_"))
                    {
                        var orderIdStr = data["cancel_order_".Length..];
                        if (Guid.TryParse(orderIdStr, out var orderId))
                        {
                            var result = await _orderApi.CancelOrderAsync(orderId);
                            if (result.success)
                            {
                                await _botClient.AnswerCallbackQuery(callbackQuery.Id, "✅ سفارش با موفقیت لغو شد");
                                
                                // حذف پیام یا به‌روزرسانی آن
                                await _botClient.EditMessageText(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    messageId: callbackQuery.Message.MessageId,
                                    text: "✅ سفارش لغو شد و از لیست حذف گردید.",
                                    replyMarkup: null
                                );
                            }
                            else
                            {
                                await _botClient.AnswerCallbackQuery(callbackQuery.Id, $"❌ خطا در لغو سفارش: {result.message}");
                            }
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
        /// <summary>
        /// شاید بهتر باشه یوزرو کش کنیم که زیاد ریکئست نفرستیم
        /// </summary>
        /// <param name="chatId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<UserRole> GetUserRoleAsync(long chatId)
        {
            var user = await _usersApi.GetUserAsync(chatId);

            if (user == null)
            {
                await _botClient.SendMessage(chatId, "کاربر یافت نشد. لطفاً ابتدا ثبت‌نام کنید.");
                throw new Exception("User not found");
            }

            return user.Role;
        }
        private async Task ShowMainMenuAsync(long chatId)
        {
            //bool isAdmin = await IsTelegramAdmin(user);
            //isAdmin = true; // for test
            ////if (isAdmin)

            if (await GetUserRoleAsync(chatId) == TallaEgg.Core.Enums.User.UserRole.Admin)
            {
                await _botClient.SendMainKeyboardForAdminAsync(chatId);
            }
            else
            {
                await _botClient.SendMainKeyboardForUserAsync(chatId);
            }
        }

        private async Task HandleAccountingMenuAsync(long chatId)
        {

            await _botClient.SendAccountingMenuKeyboard(chatId);
        }

        private async Task ShowHelpAsync(long chatId)
        {
            var role = await GetUserRoleAsync(chatId);
            
            var helpText = "❓ راهنما\n\n" +
                          "💰 نقدی: معاملات نقدی و فوری\n" +
                          "📈 آتی: معاملات آتی و قراردادهای آتی\n" +
                          "📊 حسابداری: مشاهده موجودی و تاریخچه معاملات\n" +
                          "❓ راهنما: این صفحه\n\n";

            if (role == TallaEgg.Core.Enums.User.UserRole.Admin)
            {
                helpText += BotMsgs.MsgAdminHelp + "\n\n";
            }

            helpText += "برای پشتیبانی با تیم فنی تماس بگیرید.";

            await _botClient.SendMessage(chatId, helpText);
        }
        private async Task ShowOrderHistory(long chatId, Guid userId)
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

        private async Task ShowTradeHistory(long chatId, Guid userId)
        {
            var page = await _orderApi.GetUserTradesAsync(userId, pageNumber: 1, pageSize: 5);
            if (page.Success)
            {
                var text = await TradeListHandler.BuildTradesListAsync(page.Data!, 1);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.MarkdownV2,
                    replyMarkup: TradeListHandler.BuildPagingKeyboard(page.Data!, 1, userId)
                );
            }
        }

        private async Task ShowActiveOrders(long chatId, Guid userId)
        {
            var role = await GetUserRoleAsync(chatId);
            var isAdmin = role == TallaEgg.Core.Enums.User.UserRole.Admin;

            var response = isAdmin 
                ? await _orderApi.GetAllActiveOrdersAsync()
                : await _orderApi.GetUserActiveOrdersAsync(userId);

            if (response.Success)
            {
                var text = await ActiveOrdersHandler.BuildActiveOrdersListAsync(response.Data!, isAdmin);
                var keyboard = ActiveOrdersHandler.BuildCancelOrderKeyboard(response.Data!, isAdmin);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.MarkdownV2,
                    replyMarkup: keyboard
                );
            }
            else
            {
                await _botClient.SendMessage(chatId, "خطا در دریافت سفارشات فعال: " + response.Message);
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

        /// <summary>
        /// بعد از این که خرید یا فروش انتخاب شد با این تابع نمادهای معاملاتی را برای کاربر ارسال میکنیم
        /// </summary>
        /// <param name="chatId"></param>
        /// <param name="telegramId"></param>
        /// <param name="orderSide"></param>
        /// <returns></returns>
        private async Task ShowSymbolsAsync(long chatId, long telegramId)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            // Get available assets 
            //TODO اینحا باید نمادهای معاملاتیرو از یجایی بحونیم
            // فعلا نمادهای معاملاتی به صورت HardCode
            // Mesqal Au Abshode
            //var assets = new[] { "MAUA/IRR", "XAU/IRR", "BTC/USDT", "ETH/USDT", "XAU/USD", "XAG/USD" };

            var assets = new[]
            {
                new { Symbol = "MAUA/IRR", DisplayName = "طلا آبشده / ریال" },
                //new { Symbol = "XAU/IRR", DisplayName = "انس طلا / ریال" },
                //new { Symbol = "BTC/USDT", DisplayName = "بیت‌کوین / تتر" },
                //new { Symbol = "ETH/USDT", DisplayName = "اتریوم / تتر" },
                //new { Symbol = "XAU/USD", DisplayName = "انس طلا / دلار" },
                //new { Symbol = "XAG/USD", DisplayName = "انس نقره / دلار" }
            };

            // Show available assets
            var assetButtons = new List<InlineKeyboardButton[]>();

            foreach (var asset in assets)
            {
                assetButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(asset.DisplayName, $"asset_{asset.Symbol}")
                });
            }

            assetButtons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, "back_to_main")
            });

            var keyboard = new InlineKeyboardMarkup(assetButtons.ToArray());

            await _botClient.SendMessage(chatId, BotMsgs.MsgSelectAsset, replyMarkup: keyboard);
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

            if (orderState.OrderType == OrderType.Limit)
            {
                orderState.State = "waiting_for_price";

                await _botClient.SendMessage(chatId,
                 $"لطفا قیمت رو وارد کنید",
                 replyMarkup: new ReplyKeyboardRemove());
            }
            else if (orderState.OrderType == OrderType.Market)
            {
                if (orderState.OrderSide == OrderSide.Buy && orderState.BestAskPrice.HasValue)
                {
                    orderState.Price = orderState.BestAskPrice.Value;
                }
                else if (orderState.OrderSide == OrderSide.Sell && orderState.BestBidPrice.HasValue)
                {
                    orderState.Price = orderState.BestBidPrice.Value;
                }
                else
                {
                    await _botClient.SendMessage(chatId, "خطا در دریافت بهترین قیمت بازار. لطفاً دوباره تلاش کنید.");
                    _userOrderStates.Remove(telegramId);
                    return;
                }

                await HandleOrderPriceInputAsync(chatId, telegramId, orderState.Price.ToString());
            }
        }

        private async Task HandleOrderPriceInputAsync(long chatId, long telegramId, string priceStr)
        {
            if (!_userOrderStates.ContainsKey(telegramId))
            {
                await _botClient.SendMessage(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            if (!decimal.TryParse(priceStr, out var price) || price <= 0)
            {
                await _botClient.SendMessage(chatId, "لطفاً قیمت معتبر وارد کنید.");
                return;
            }

            var orderState = _userOrderStates[telegramId];
            orderState.Price = price;
            orderState.State = "";

            var confirmationMsg = "";

            // اگر طلای آبشده را انتخاب کرد باید مثقال را به گرم تبدیل کنیم
            if (orderState.Asset == "MAUA/IRR")
            {
                orderState.Price /= 4.3318m;
                confirmationMsg = BotMsgs.MsgOrderConfirmation_MAUA_IRR;
            }
            else
            {
                confirmationMsg = BotMsgs.MsgOrderConfirmation;
            }
            var totalValue = orderState.Amount * orderState.Price;

            // Check user's balance for the asset

            var assetToCheck = orderState.OrderSide == OrderSide.Buy
                ? orderState.Asset.Split('/')[1] : orderState.Asset.Split('/')[0];

            //var (balanceSuccess, balanceMessage, balance) = await _walletApi.GetBalanceAsync(orderState.UserId, assetToCheck);
            var (balanceCheckSuccess, balanceMessage, hasSufficientBalance) = await _walletApi.ValidateBalanceAsync(orderState.UserId, assetToCheck, totalValue);

            if (!balanceCheckSuccess || !hasSufficientBalance)
            {
                var availableBalance = balanceMessage;
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

            var confirmationMessage = string.Format(confirmationMsg,
                orderState.Asset,
                orderState.OrderSide,
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
                Side = orderState.OrderSide,
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

    }
}