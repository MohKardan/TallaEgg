using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.Order;
using TallaEgg.Core.Services;
using TallaEgg.Core.Utilties;
using TallaEgg.Infrastructure;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram;
using TallaEgg.TelegramBot.Infrastructure.Handlers;
using TallaEgg.TelegramBot.Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
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
        private readonly ILogger<BotHandler> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly OrderApiClient _orderApi;
        private readonly UsersApiClient _usersApi;
        private readonly AffiliateApiClient _affiliateApi;
        private readonly WalletApiClient _walletApi;
        private readonly TelegramLoggerService _telegramLogger;
        private readonly IVersionService _versionService;

        private readonly Dictionary<long, OrderState> _userOrderStates = new();

        private bool _requireReferralCode;
        private string _defaultReferralCode;

        public BotHandler(ILogger<BotHandler> logger,
                         ITelegramBotClient botClient, OrderApiClient orderApi, UsersApiClient usersApi,
                         AffiliateApiClient affiliateApi, WalletApiClient walletApi, TelegramLoggerService telegramLogger, IVersionService versionService,
                         bool requireReferralCode = false, string defaultReferralCode = "ADMIN2024")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _botClient = botClient;
            _orderApi = orderApi;
            _usersApi = usersApi;
            _affiliateApi = affiliateApi;
            _walletApi = walletApi;
            _telegramLogger = telegramLogger;
            _requireReferralCode = requireReferralCode;
            _defaultReferralCode = defaultReferralCode;
            _versionService = versionService;

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
                        await _telegramLogger.ErrorAsync(ex, "Error in cleanup");
                        Console.WriteLine($"Error in cleanup: {ex.Message}");
                    }
                }
            });
            NotifyUpdateToAllUsers();
        }

        public async Task HandleMessageAsync(Message message)
        {
            try
            {
                var chatId = message.Chat.Id;
                var telegramId = message.From?.Id ?? 0;
                await _telegramLogger.LogAsync<Message>($"✔➕ new message:",message);


                message.Text = TallaEgg.Core.Utilties.Utils.ConvertPersianDigitsToEnglish(message.Text);

                // Check if user exists
                var user = await _usersApi.GetUserAsync(telegramId);

                if (user == null)
                {
                    await _botClient.SendMessage(chatId, "حساب شما پیدا نشد. لطفاً ابتدا با دستور شروع ثبت‌نام کنید.");
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
                         string.Format(BotMsgs.MsgAccountNotApproved, user.FirstName).AutoRtl()
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
                await _telegramLogger.ErrorAsync(ex, "❌ Error in HandleUpdateAsync");

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

                    await _botClient.DeleteMessage(chatId, message.Id);

                    await _botClient.SendMessage(chatId,
                                                 $"لطفاً مقدار را وارد کنید.",
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

                // هر دو مسیر شارژ به یک پیام واحد می‌رسند: در حال حاضر درگاه پرداخت
                // وجود ندارد و شارژ حساب توسط طلافروشی انجام می‌شود.
                case InlineCallBackData.charge_card:
                case InlineCallBackData.charge_bank:
                    await _botClient.SendMessage(chatId, BotMsgs.MsgChargeInfo);
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

                            await _botClient.DeleteMessage(chatId, message.Id);

                            // قیمت می‌تواند خالی باشد (وقتی در آن سمت بازار سفارشی نیست).
                            // نمایش صفر گمراه‌کننده است، پس پیام صریح نشان داده می‌شود.
                            string FormatPrice(decimal? price) => price.HasValue
                                ? $"{PersianFormat.Number(price.Value)} تومان"
                                : BotMsgs.MsgPriceNotAvailable;

                            await _botClient.SendMessage(chatId,
                                            string.Format(BotMsgs.MsgBestPrices,
                                                FormatPrice(apiResponse.Data.BestBidPrice),
                                                FormatPrice(apiResponse.Data.BestAskPrice)));

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
                                replyMarkup: OrderListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // بستن "لطفاً چند لحظه صبر کنید…" روی دکمه
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

                            // uid همان کاربری است که فهرست را می‌بیند؛ برای تعیین اینکه هر
                            // معامله از دید او خرید بوده یا فروش لازم است.
                            var text = await TradeListHandler.BuildTradesListAsync(page.Data!, pageNum, uid);

                            // ویرایش پیام قبلی
                            await _botClient.EditMessageText(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: text,
                                replyMarkup: TradeListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // بستن "لطفاً چند لحظه صبر کنید…" روی دکمه
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
                                await _botClient.AnswerCallbackQuery(callbackQuery.Id, "✅ سفارش شما لغو شد و مبلغ درگیر آزاد گردید.");
                                
                                // حذف پیام یا به‌روزرسانی آن
                                await _botClient.EditMessageText(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    messageId: callbackQuery.Message.MessageId,
                                    text: "✅ سفارش لغو شد و از فهرست حذف گردید.",
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

                            // بستن "لطفاً چند لحظه صبر کنید…" روی دکمه
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
                await _botClient.SendMessage(chatId, "حساب شما پیدا نشد. لطفاً ابتدا با دستور شروع ثبت‌نام کنید.");
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
            if (await GetUserRoleAsync(chatId) == TallaEgg.Core.Enums.User.UserRole.Admin)
            {
                await _botClient.SendAccountingMenuKeyboardForAdmin(chatId);
            }
            else
            {
                await _botClient.SendAccountingMenuKeyboard(chatId);
            }
        }

        private async Task ShowHelpAsync(long chatId)
        {
            var role = await GetUserRoleAsync(chatId);
            
            var helpText = BotMsgs.MsgUserHelp;

            if (role == TallaEgg.Core.Enums.User.UserRole.Admin)
            {
                helpText += BotMsgs.MsgAdminHelp + "\n\n";
            }

            helpText += BotMsgs.MsgSupportFooter;

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
                    replyMarkup: OrderListHandler.BuildPagingKeyboard(page.Data!, 1, userId)
                );
            }
        }

        private async Task ShowTradeHistory(long chatId, Guid userId)
        {
            var page = await _orderApi.GetUserTradesAsync(userId, pageNumber: 1, pageSize: 5);
            if (page.Success)
            {
                var text = await TradeListHandler.BuildTradesListAsync(page.Data!, 1, userId);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: text,
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

                // متن ساده ارسال می‌شود؛ با MarkdownV2 نشانه‌های قالب‌بندی escape می‌شدند
                // و به‌صورت ستارهٔ خام به کاربر نمایش داده می‌شدند.
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: text,
                    replyMarkup: keyboard
                );
            }
            else
            {
                await _botClient.SendMessage(chatId,
                    string.Format(BotMsgs.MsgActiveOrdersFailed, response.Message));
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
                    stringBuilder.Append(BotMsgs.MsgBalanceHeader);

                    foreach (var item in res.Data)
                    {
                        var code = item.Asset;
                        var unit = PersianFormat.Unit(code);

                        // نام فارسی دارایی؛ کد لاتین هرگز به کاربر نشان داده نمی‌شود.
                        stringBuilder.Append(string.Format(BotMsgs.MsgBalanceRow,
                            PersianFormat.Asset(code),
                            $"{PersianFormat.Amount(item.Balance, code)} {unit}",
                            $"{PersianFormat.Amount(item.LockedBalance, code)} {unit}"));

                        // در مدل اعتباری موجودی آزاد می‌تواند منفی شود (کاربر با اعتبار
                        // معامله کرده است). عدد منفی بدون توضیح گیج‌کننده است، پس مبلغ
                        // بدهی به‌صورت مثبت و با برچسب صریح نمایش داده می‌شود.
                        if (item.Balance < 0)
                        {
                            stringBuilder.Append(string.Format(BotMsgs.MsgBalanceDebtNote,
                                $"{PersianFormat.Amount(-item.Balance, code)} {unit}"));
                        }

                        stringBuilder.AppendLine();
                    }

                    stringBuilder.Append(BotMsgs.MsgBalanceFooter);

                    await _botClient.SendMessage(chatId, stringBuilder.ToString());

                }
                else
                {
                    await _botClient.SendMessage(chatId, BotMsgs.MsgNoWallet);

                }
            }
            else
            {

                await _botClient.SendMessage(chatId, res.Message);
            }


        }

        /// <summary>
        /// نمایش نمادهای معاملاتی فعال به کاربر پس از انتخاب نوع سفارش
        /// Display active trading symbols to user after order type selection
        /// </summary>
        /// <param name="chatId">شناسه چت تلگرام</param>
        /// <param name="telegramId">شناسه کاربر تلگرام</param>
        /// <returns>نتیجه عملیات ارسال پیام</returns>
        private async Task<bool> ShowSymbolsAsync(long chatId, long telegramId)
        {
            try
            {
                // بررسی وجود state کاربر
                if (!_userOrderStates.ContainsKey(telegramId))
                {
                    _logger.LogWarning("User order state not found for telegramId: {TelegramId}", telegramId);
                    await SendErrorMessageAsync(chatId, "خطا در پردازش سفارش. لطفاً از منوی اصلی دوباره شروع کنید.");
                    return false;
                }

                // دریافت جفت‌های معاملاتی فعال
                var activeTradingPairs = GetActiveTradingPairs();

                if (!activeTradingPairs.Any())
                {
                    _logger.LogError("No active trading pairs found");
                    await SendErrorMessageAsync(chatId, "در حال حاضر نمادی برای معامله فعال نیست. لطفاً بعداً تلاش کنید.");
                    return false;
                }

                // ساخت دکمه‌های نمادهای معاملاتی
                var symbolButtons = CreateSymbolButtons(activeTradingPairs);

                // اضافه کردن دکمه بازگشت
                symbolButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, InlineCallBackData.BackToMain)
                });

                var keyboard = new InlineKeyboardMarkup(symbolButtons.ToArray());

                // ارسال پیام با مدیریت خطا
                var message = await SendMessageWithRetryAsync(chatId, BotMsgs.MsgSelectAsset, keyboard);

                if (message == null)
                {
                    _logger.LogError("Failed to send symbol selection message to chatId: {ChatId}", chatId);
                    return false;
                }

                _logger.LogInformation("Successfully showed {Count} trading symbols to user {TelegramId}",
                    activeTradingPairs.Count, telegramId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShowSymbolsAsync for telegramId: {TelegramId}, chatId: {ChatId}",
                    telegramId, chatId);

                try
                {
                    await SendErrorMessageAsync(chatId, "خطای سیستمی رخ داده است. لطفاً دوباره تلاش کنید.");
                }
                catch (Exception innerEx)
                {
                    _logger.LogCritical(innerEx, "Failed to send error message after ShowSymbolsAsync failure");
                }

                return false;
            }
        }

        /// <summary>
        /// دریافت لیست جفت‌های معاملاتی فعال با validation
        /// </summary>
        /// <returns>لیست جفت‌های معاملاتی فعال</returns>
        private List<TradingPairInfo> GetActiveTradingPairs()
        {
            try
            {
                if (CurrenciesConstant.AllTradingPairs == null)
                {
                    _logger.LogError("CurrenciesConstant.AllTradingPairs is null");
                    return new List<TradingPairInfo>();
                }

                var activePairs = CurrenciesConstant.AllTradingPairs
                    .Where(pair => pair != null &&
                                  pair.IsActive &&
                                  !string.IsNullOrWhiteSpace(pair.Symbol) &&
                                  !string.IsNullOrWhiteSpace(pair.PersianName))
                    .ToList();

                _logger.LogDebug($"Found {activePairs.Count} active trading pairs");
                return activePairs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active trading pairs");
                return new List<TradingPairInfo>();
            }
        }

        /// <summary>
        /// ساخت دکمه‌های نمادهای معاملاتی با محدودیت تعداد
        /// </summary>
        /// <param name="tradingPairs">لیست جفت‌های معاملاتی</param>
        /// <returns>لیست دکمه‌های inline keyboard</returns>
        private List<InlineKeyboardButton[]> CreateSymbolButtons(List<TradingPairInfo> tradingPairs)
        {
            var buttons = new List<InlineKeyboardButton[]>();

            try
            {
                const int maxButtonsPerPage = 10; // محدودیت تعداد دکمه‌ها
                var pairsToShow = tradingPairs.Take(maxButtonsPerPage);

                foreach (var pair in pairsToShow)
                {
                    try
                    {
                        // validation اضافی برای هر pair
                        if (string.IsNullOrWhiteSpace(pair.Symbol) || string.IsNullOrWhiteSpace(pair.PersianName))
                        {
                            _logger.LogWarning("Invalid trading pair data: Symbol={Symbol}, PersianName={PersianName}",
                                pair.Symbol, pair.PersianName);
                            continue;
                        }

                        var callbackData = $"{InlineCallBackData.AssetPrefix}_{pair.Symbol}";

                        // بررسی طول callback data (محدودیت تلگرام: 64 کاراکتر)
                        if (callbackData.Length > 64)
                        {
                            _logger.LogWarning("Callback data too long for symbol {Symbol}: {Length} characters",
                                pair.Symbol, callbackData.Length);
                            continue;
                        }

                        buttons.Add(new[]
                        {
                    InlineKeyboardButton.WithCallbackData(pair.PersianName, callbackData)
                });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating button for trading pair {Symbol}", pair.Symbol ?? "Unknown");
                    }
                }

                if (tradingPairs.Count > maxButtonsPerPage)
                {
                    _logger.LogInformation("Showing {Shown} out of {Total} trading pairs due to pagination limit",
                        maxButtonsPerPage, tradingPairs.Count);

                    // TODO: اضافه کردن دکمه‌های pagination برای صفحه‌بندی
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating symbol buttons");
            }

            return buttons;
        }

        /// <summary>
        /// ارسال پیام با retry mechanism
        /// </summary>
        /// <param name="chatId">شناسه چت</param>
        /// <param name="text">متن پیام</param>
        /// <param name="keyboard">کیبورد inline (اختیاری)</param>
        /// <param name="maxRetries">حداکثر تعداد تلاش مجدد</param>
        /// <returns>پیام ارسال شده یا null در صورت شکست</returns>
        private async Task<Message?> SendMessageWithRetryAsync(long chatId, string text,
            InlineKeyboardMarkup? keyboard = null, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var message = await _botClient.SendMessage(chatId, text, replyMarkup: keyboard);
                    return message;
                }
                catch (ApiRequestException apiEx) when (apiEx.ErrorCode == 429) // Rate limiting
                {
                    _logger.LogWarning("Rate limited on attempt {Attempt}, waiting before retry", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2)); // Exponential backoff
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send message on attempt {Attempt}/{MaxAttempts} to chatId: {ChatId}",
                        attempt, maxRetries, chatId);

                    if (attempt == maxRetries)
                        return null;

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            return null;
        }

        /// <summary>
        /// ارسال پیام خطا استاندارد
        /// </summary>
        /// <param name="chatId">شناسه چت</param>
        /// <param name="errorMessage">پیام خطا</param>
        private async Task SendErrorMessageAsync(long chatId, string errorMessage)
        {
            try
            {
                await _botClient.SendMessage(chatId, $"❌ {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send error message to chatId: {ChatId}", chatId);
            }
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

            // در حالت مظنه‌ای اصلاً قیمتی از مشتری پرسیده نمی‌شود (issue #48): قیمت همان
            // مظنهٔ منتشرشدهٔ ادمین است. این کل ابهام مثقال/گرم را از جریان مشتری حذف
            // می‌کند — مشتری فقط مقدار می‌گوید.
            var activeQuote = await _orderApi.GetActiveQuoteAsync(orderState.Asset);

            if (activeQuote is not null)
            {
                // قیمت مظنه بر حسب گرم ذخیره شده؛ برای نمایش به مثقال تبدیل می‌شود، چون
                // کاربر قیمت را با همان واحد می‌شناسد.
                var pricePerGram = orderState.OrderSide == OrderSide.Buy
                    ? activeQuote.SellPrice   // مشتری می‌خرد ⇒ قیمت فروش ادمین
                    : activeQuote.BuyPrice;   // مشتری می‌فروشد ⇒ قیمت خرید ادمین

                orderState.Price = pricePerGram;
                orderState.State = "";

                // از همان قالب‌های تأیید سفارش استفاده می‌شود که برای مسیر عادی نوشته
                // شده‌اند. یک قالب دوم برای همان اطلاعات، دو جا برای نگهداری می‌ساخت — و
                // کاربر هم انتظار دارد پیام تأیید همیشه یک شکل باشد.
                var quoteIsGold = orderState.Asset == CurrenciesConstant.MAUA_IRT;
                var quoteBaseAsset = orderState.Asset.Split('/')[0];
                var quoteAmountText =
                    $"{PersianFormat.Amount(orderState.Amount, quoteBaseAsset)} {PersianFormat.Unit(quoteBaseAsset)}";
                var quoteSideText = TallaEgg.Core.Utilties.Utils.GetEnumDescription(orderState.OrderSide);
                var quoteTotal = CurrenciesConstant.RoundToCurrencyPrecision(
                    orderState.Amount * pricePerGram, CurrenciesConstant.Toman);

                var quoteMessage = quoteIsGold
                    ? string.Format(BotMsgs.MsgOrderConfirmationGold,
                        PersianFormat.Symbol(orderState.Asset),
                        quoteSideText,
                        quoteAmountText,
                        PersianFormat.Number(pricePerGram * CurrenciesConstant.GramsPerMesghal),
                        PersianFormat.Number(pricePerGram),
                        PersianFormat.Number(quoteTotal))
                    : string.Format(BotMsgs.MsgOrderConfirmation,
                        PersianFormat.Symbol(orderState.Asset),
                        quoteSideText,
                        quoteAmountText,
                        PersianFormat.Number(pricePerGram),
                        PersianFormat.Number(quoteTotal));

                await _botClient.SendMessage(chatId, quoteMessage,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new InlineKeyboardButton[]
                        {
                            InlineKeyboardButton.WithCallbackData(BotBtns.BtnConfirm, InlineCallBackData.confirm_order),
                            InlineKeyboardButton.WithCallbackData(BotBtns.BtnCancel, InlineCallBackData.cancel_order)
                        }
                    }));

                return;
            }

            if (orderState.OrderType == OrderType.Limit)
            {
                orderState.State = "waiting_for_price";

                // برای طلای آبشده قیمت «یک مثقال» خواسته می‌شود؛ واحد صریح ذکر می‌شود.
                var pricePrompt = orderState.Asset == CurrenciesConstant.MAUA_IRT
                    ? BotMsgs.MsgEnterPriceGold
                    : string.Format(BotMsgs.MsgEnterPrice, PersianFormat.Symbol(orderState.Asset));

                await _botClient.SendMessage(chatId, pricePrompt,
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

            // قیمتی که کاربر وارد می‌کند برای «یک مثقال» است؛ برای طلای آبشده به قیمت
            // «هر گرم» تبدیل و ذخیره می‌شود. عدد ورودی نگه داشته می‌شود تا در پیام تایید
            // هم نمایش داده شود، وگرنه کاربر عددی غیر از ورودی خود می‌بیند.
            var enteredPricePerMesghal = price;
            var isGold = orderState.Asset == CurrenciesConstant.MAUA_IRT;

            if (isGold)
            {
                orderState.Price /= 4.3318m;
                confirmationMsg = BotMsgs.MsgOrderConfirmationGold;
            }
            else
            {
                confirmationMsg = BotMsgs.MsgOrderConfirmation;
            }
            var totalValue = orderState.Amount * orderState.Price;

            var validateCreditAndBalance =
                await _walletApi.ValidateCreditAndBalanceAsync(orderState.UserId, orderState.Asset, orderState.Amount, orderState.Price);

            var hasSufficientBalance = orderState.OrderSide == OrderSide.Buy
                ? validateCreditAndBalance.HasSufficientCreditAndBalanceQuote : validateCreditAndBalance.HasSufficientCreditAndBalanceBase;

            var isAdmin = await GetUserRoleAsync(chatId) == TallaEgg.Core.Enums.User.UserRole.Admin;

            if (!isAdmin)    
            if (!validateCreditAndBalance.Success || !hasSufficientBalance)
            {
                var backBtn = new KeyboardButton(BotBtns.BtnBack);
                await _botClient.SendMessage(chatId,
                    string.Format(BotMsgs.MsgInsufficientBalance, validateCreditAndBalance.Message),
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

            // مقادیر پیش از درج در پیام فارسی‌سازی می‌شوند: نماد به نام فارسی، نوع سفارش
            // به «خرید/فروش»، و اعداد با ارقام فارسی و محافظ راست‌به‌چپ.
            var baseAsset = orderState.Asset.Split('/')[0];
            var amountText = $"{PersianFormat.Amount(orderState.Amount, baseAsset)} {PersianFormat.Unit(baseAsset)}";
            var sideText = TallaEgg.Core.Utilties.Utils.GetEnumDescription(orderState.OrderSide);

            // قالب طلا یک آرگومان بیشتر دارد: قیمت هر مثقال و قیمت هر گرم.
            var confirmationMessage = isGold
                ? string.Format(confirmationMsg,
                    PersianFormat.Symbol(orderState.Asset),
                    sideText,
                    amountText,
                    PersianFormat.Number(enteredPricePerMesghal),
                    PersianFormat.Number(orderState.Price),
                    PersianFormat.Number(totalValue))
                : string.Format(confirmationMsg,
                    PersianFormat.Symbol(orderState.Asset),
                    sideText,
                    amountText,
                    PersianFormat.Number(orderState.Price),
                    PersianFormat.Number(totalValue));

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

            try
            {
                // در حالت مظنه‌ای، مشتری مظنهٔ منتشرشده را می‌پذیرد و قیمت نمی‌فرستد
                // (issue #48). قیمت را سرور از مظنه می‌خواند، پس امکان اختلاف بین آنچه
                // مشتری دیده و آنچه معامله می‌شود وجود ندارد.
                //
                // اگر مظنه‌ای منتشر نشده باشد، به مسیر سفارش عادی برمی‌گردیم — همان رفتار
                // دفتر سفارش که برای نمادهای دیگر و برای زمانی که این نماد به حالت
                // OrderBook برود لازم است.
                var quote = await _orderApi.GetActiveQuoteAsync(orderState.Asset);

                var (orderSuccess, orderMessage) = quote is not null
                    ? await _orderApi.AcceptQuoteAsync(
                        orderState.UserId, orderState.Asset, orderState.OrderSide, orderState.Amount)
                    : await _orderApi.SubmitOrderAsync(new OrderDto
                    {
                        Asset = orderState.Asset,
                        Amount = orderState.Amount,
                        Price = orderState.Price,
                        UserId = orderState.UserId,
                        Side = orderState.OrderSide,
                        Type = orderState.OrderType,
                        TradingType = orderState.TradingType
                    });

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
        /// <summary>
        /// متن اعلان استارتاپ را می‌سازد: اگر نسخه واقعاً تغییر کرده باشد پیام آپدیت
        /// (به همراه خلاصه تغییرات در صورت وجود)، وگرنه پیام «دوباره در دسترس است».
        /// </summary>
        private (string Message, bool IsVersionChange) BuildStartupAnnouncement()
        {
            var currentVersion = _versionService.GetCurrentVersion();
            var lastAnnounced = _versionService.GetLastAnnouncedVersion();
            var isVersionChange = !string.Equals(currentVersion, lastAnnounced, StringComparison.Ordinal);

            var message = isVersionChange
                ? string.Format(BotMsgs.MsgBotUpdated, currentVersion, ReleaseNotes.GetSummaryFor(currentVersion))
                : string.Format(BotMsgs.MsgBotBackOnline, currentVersion);

            return (message, isVersionChange);
        }

        public async Task NotifyUpdate(User user)
        {
            var (message, _) = BuildStartupAnnouncement();
            await _botClient.SendMessage(user.Id, message);
        }

        public async Task NotifyUpdateToAllUsers()
        {
            try
            {
                // Local runs restart constantly; set BOT_SUPPRESS_STARTUP_ANNOUNCEMENT=1
                // to avoid messaging every real user on each restart while developing.
                if (Environment.GetEnvironmentVariable("BOT_SUPPRESS_STARTUP_ANNOUNCEMENT") == "1")
                {
                    _logger.LogInformation("Startup announcement suppressed (BOT_SUPPRESS_STARTUP_ANNOUNCEMENT=1).");
                    return;
                }

                var currentVersion = _versionService.GetCurrentVersion();
                var (message, isVersionChange) = BuildStartupAnnouncement();

                // صبر کردن ۳۰ ثانیه برای اطمینان از اینکه سرویس کاربر اجرا شده باشد وگرنه خطا میدهد
                await Task.Delay(30000);

                var usersResponse = await _usersApi.GetUsersAsync();

                var users = usersResponse.Data;
                if (users == null || users.TotalCount == 0)
                    return;

                _logger.LogInformation(
                    "Broadcasting startup announcement to {Count} user(s). Version={Version}, IsVersionChange={IsVersionChange}",
                    users.TotalCount, currentVersion, isVersionChange);

                foreach (var user in users.Items)
                {
                    try
                    {
                        await _botClient.SendMessage(user.TelegramId, message);

                        // ⏱ جلوگیری از Rate Limit تلگرام
                        await Task.Delay(50);
                    }
                    catch (Exception ex)
                    {
                        // حتماً لاگ بگیر
                        _logger.LogWarning(
                            ex,
                            "Failed to send startup announcement to user {UserId}",
                            user.Id
                        );
                    }
                }

                // Record the announced version only after a version change has actually been
                // broadcast, so the next restart reports "back online" instead of "updated".
                if (isVersionChange)
                    _versionService.SaveAnnouncedVersion(currentVersion);
            }
            catch (Exception ex)
            {
                // This runs fire-and-forget from the constructor; without this catch a failure
                // would surface as an unobserved task exception.
                _logger.LogError(ex, "Startup announcement broadcast failed.");
            }
        }

    }
}