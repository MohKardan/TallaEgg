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
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram;
using TallaEgg.TelegramBot.Infrastructure.Handlers;
using TallaEgg.TelegramBot.Infrastructure.Messages;
using TallaEgg.TelegramBot.Infrastructure.Messaging;
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
    public partial class BotHandler : IBotHandler
    {
        private readonly ILogger<BotHandler> _logger;

        /// <summary>
        /// Everything this handler says to a chat goes through here, so a test can record
        /// it instead of Telegram receiving it (issue #65).
        /// </summary>
        private readonly IBotMessenger _messenger;

        /// <summary>
        /// Retained only for the chat-administrator lookup, which is not a messaging
        /// operation and so is deliberately absent from <see cref="IBotMessenger"/>.
        /// </summary>
        private readonly ITelegramBotClient _botClient;

        // Interfaces, not the concrete HTTP clients: a conversation test supplies known
        // answers instead of standing up five services (issue #65).
        private readonly IOrderApiClient _orderApi;
        private readonly IUsersApiClient _usersApi;
        private readonly IAffiliateApiClient _affiliateApi;
        private readonly IWalletApiClient _walletApi;
        private readonly ITelegramLogger _telegramLogger;
        private readonly IVersionService _versionService;

        /// <summary>
        /// Where each customer is in the middle of placing an order. Injected rather than
        /// owned, so a test can place a customer mid-flow and assert the state is cleared
        /// afterwards (issue #65).
        /// </summary>
        private readonly IConversationStore _conversations;

        private bool _requireReferralCode;
        private string _defaultReferralCode;

        public BotHandler(ILogger<BotHandler> logger,
                         ITelegramBotClient botClient, IBotMessenger messenger,
                         IConversationStore conversations,
                         IOrderApiClient orderApi, IUsersApiClient usersApi,
                         IAffiliateApiClient affiliateApi, IWalletApiClient walletApi,
                         ITelegramLogger telegramLogger, IVersionService versionService,
                         bool requireReferralCode = false, string defaultReferralCode = "ADMIN2024")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _botClient = botClient;
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
            _orderApi = orderApi;
            _usersApi = usersApi;
            _affiliateApi = affiliateApi;
            _walletApi = walletApi;
            _telegramLogger = telegramLogger;
            _requireReferralCode = requireReferralCode;
            _defaultReferralCode = defaultReferralCode;
            _versionService = versionService;

        }

        /// <summary>
        /// Begins the background work: the hourly conversation sweep and the startup
        /// announcement.
        ///
        /// Called by the hosted service rather than from the constructor. Constructing an
        /// object should not start threads or send messages to every customer — it made
        /// the handler impossible to build in a test, and it ran before the rest of the
        /// container had finished being wired.
        /// </summary>
        public void Start(CancellationToken cancellationToken = default)
        {
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
                    try
                    {
                        var removed = _conversations.ClearCompleted();
                        if (removed > 0)
                            _logger.LogInformation("Swept {Count} completed conversations.", removed);
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        await _telegramLogger.ErrorAsync(ex, "Error in cleanup");
                        _logger.LogError(ex, "Error sweeping completed conversations.");
                    }
                }
            }, cancellationToken);

            _ = NotifyUpdateToAllUsers();
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
                    await _messenger.SendAsync(chatId, "حساب شما پیدا نشد. لطفاً ابتدا با دستور شروع ثبت‌نام کنید.");
                    await HandleNewUserAsync(chatId, telegramId, message);
                    return;
                }

                // Keyed on the Telegram user id, as every other access is. This one line
                // used the chat id; the two are equal in a private chat, so it worked, but
                // the entry was written under one key and read under another anywhere else.
                _conversations.GetOrStart(telegramId, user.Id);

                if (string.IsNullOrEmpty(user?.PhoneNumber))
                {
                    await HandlePhoneNumberRequestAsync(chatId, telegramId, message);
                    return;
                }

                if (user.Status != TallaEgg.Core.Enums.User.UserStatus.Approved)
                {
                    await _messenger.SendAsync(
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
                        await _messenger.SendAsync(chatId, BotMsgs.MsgEnterInvite);
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
                await _messenger.SendContactKeyboardAsync(chatId);

                //else
                //{
                //    await _messenger.SendAsync(chatId, $"خطا در استفاده از کد دعوت: {useMessage}");
                //}
            }
            else
            {
                await _messenger.SendAsync(chatId, $"خطا در ثبت‌نام: {regMessage}");
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
                    await _messenger.SendAsync(chatId, BotMsgs.MsgPhoneSuccess,
                        replyMarkup: new ReplyKeyboardRemove());
                    await ShowMainMenuAsync(chatId);
                    // Looking the admins up needs the raw client; deciding what they see
                    // does not. Splitting the two keeps the message itself testable.
                    var adminIds = await _botClient.GetAdminUserIdsAsync(Constants.GroupId);
                    await _messenger.SendApproveOrRejectUserToAdminsKeyboard(adminIds, response.Data);
                }
                else
                {
                    await _messenger.SendAsync(chatId, response.Message);
                }
            }
            else
            {
                await _messenger.SendContactKeyboardAsync(chatId);
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

                    // Starts the conversation if the customer has none: this is the first
                    // step of the order flow, so there is nothing yet to find.
                    _conversations.GetOrStart(telegramId, userId).OrderType = orderType;

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
                    if (_conversations.TryGet(telegramId, out var conversation))
                    {
                        var orderState = conversation;
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

                    if (!_conversations.TryGet(telegramId, out var sideConversation))
                    {
                        // The customer tapped a button from a message whose conversation is
                        // gone — after a restart, or after the flow already finished. Sending
                        // them back to the menu beats a NullReferenceException.
                        await _messenger.SendAsync(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                        break;
                    }

                    sideConversation.OrderSide = orderSide;
                    sideConversation.State = "waiting_for_amount";

                    await _messenger.DeleteAsync(chatId, message.Id);

                    await _messenger.SendAsync(chatId,
                                                 $"لطفاً مقدار را وارد کنید.",
                                                 replyMarkup: new ReplyKeyboardRemove());

                    break;

                case InlineCallBackData.confirm_order:
                    await HandleOrderConfirmationAsync(chatId, telegramId);
                    break;

                case InlineCallBackData.cancel_order:
                    _conversations.Clear(telegramId);
                    await ShowMainMenuAsync(chatId);
                    break;

                // هر دو مسیر شارژ به یک پیام واحد می‌رسند: در حال حاضر درگاه پرداخت
                // وجود ندارد و شارژ حساب توسط طلافروشی انجام می‌شود.
                case InlineCallBackData.charge_card:
                case InlineCallBackData.charge_bank:
                    await _messenger.SendAsync(chatId, BotMsgs.MsgChargeInfo);
                    break;

                case InlineCallBackData.back_to_main:
                    _conversations.Clear(telegramId);
                    await ShowMainMenuAsync(chatId);
                    break;

                default:
                    // Handle asset selection
                    if (data.StartsWith("asset_"))
                    {
                        var asset = data.Substring(6); // Remove "asset_" prefix

                        if (!_conversations.TryGet(telegramId, out var assetConversation))
                        {
                            await _messenger.SendAsync(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                            return;
                        }

                        assetConversation.Asset = asset;
                        assetConversation.State = "waiting_for_select_side";

                        TallaEgg.Core.DTOs.ApiResponse<BestPricesDto> apiResponse = await _orderApi.GetBestPricesAsync(asset);
                        if (apiResponse != null && apiResponse.Success)
                        {
                            apiResponse.Data.BestBidPrice *= 4.3318m;
                            apiResponse.Data.BestAskPrice *= 4.3318m;

                            await _messenger.DeleteAsync(chatId, message.Id);

                            await _messenger.SendAsync(chatId, BestPricesMessage.Build(
                                apiResponse.Data.BestBidPrice, apiResponse.Data.BestAskPrice));

                            assetConversation.BestBidPrice = apiResponse.Data.BestBidPrice;
                            assetConversation.BestAskPrice = apiResponse.Data.BestAskPrice;
                        }

                        await _messenger.SendSpotSideMenuKeyboard(chatId);

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
                            await _messenger.EditTextAsync(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: text,
                                replyMarkup: OrderListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // بستن "لطفاً چند لحظه صبر کنید…" روی دکمه
                            await _messenger.AnswerCallbackAsync(callbackQuery.Id);
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
                            await _messenger.EditTextAsync(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: text,
                                replyMarkup: TradeListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // بستن "لطفاً چند لحظه صبر کنید…" روی دکمه
                            await _messenger.AnswerCallbackAsync(callbackQuery.Id);
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
                                await _messenger.AnswerCallbackAsync(callbackQuery.Id, "✅ سفارش شما لغو شد و مبلغ درگیر آزاد گردید.");
                                
                                // حذف پیام یا به‌روزرسانی آن
                                await _messenger.EditTextAsync(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    messageId: callbackQuery.Message.MessageId,
                                    text: "✅ سفارش لغو شد و از فهرست حذف گردید.",
                                    replyMarkup: null
                                );
                            }
                            else
                            {
                                await _messenger.AnswerCallbackAsync(callbackQuery.Id, $"❌ خطا در لغو سفارش: {result.message}");
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
                            await _messenger.EditTextAsync(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: text,
                                parseMode: ParseMode.MarkdownV2,
                                replyMarkup: UserListHandler.BuildPagingKeyboard(page.Data!, newPage, query)
                            );

                            // بستن "لطفاً چند لحظه صبر کنید…" روی دکمه
                            await _messenger.AnswerCallbackAsync(callbackQuery.Id);
                        }
                    }
                    break;
            }

            await _messenger.AnswerCallbackAsync(callbackQuery.Id);
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
                await _messenger.SendAsync(chatId, "حساب شما پیدا نشد. لطفاً ابتدا با دستور شروع ثبت‌نام کنید.");
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
                await _messenger.SendMainKeyboardForAdminAsync(chatId);
            }
            else
            {
                await _messenger.SendMainKeyboardForUserAsync(chatId);
            }
        }

        private async Task HandleAccountingMenuAsync(long chatId)
        {
            if (await GetUserRoleAsync(chatId) == TallaEgg.Core.Enums.User.UserRole.Admin)
            {
                await _messenger.SendAccountingMenuKeyboardForAdmin(chatId);
            }
            else
            {
                await _messenger.SendAccountingMenuKeyboard(chatId);
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

            await _messenger.SendAsync(chatId, helpText);
        }
        private async Task ShowOrderHistory(long chatId, Guid userId)
        {

            var page = await _orderApi.GetUserOrdersAsync(userId, pageNumber: 1, pageSize: 5);
            if (page.Success)
            {
                var text = await OrderListHandler.BuildOrdersListAsync(page.Data!, 1);

                await _messenger.SendAsync(
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

                await _messenger.SendAsync(
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
                await _messenger.SendAsync(
                    chatId: chatId,
                    text: text,
                    replyMarkup: keyboard
                );
            }
            else
            {
                await _messenger.SendAsync(chatId,
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
                    await _messenger.SendAsync(chatId, WalletBalanceMessage.Build(res.Data));

                }
                else
                {
                    await _messenger.SendAsync(chatId, BotMsgs.MsgNoWallet);

                }
            }
            else
            {

                await _messenger.SendAsync(chatId, res.Message);
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
                if (!_conversations.TryGet(telegramId, out var conversation))
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
        /// Sends a message, retrying on transient failure.
        /// </summary>
        /// <returns>The sent message's id, or null if every attempt failed.</returns>
        private async Task<int?> SendMessageWithRetryAsync(long chatId, string text,
            InlineKeyboardMarkup? keyboard = null, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await _messenger.SendAsync(chatId, text, replyMarkup: keyboard);
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
                await _messenger.SendAsync(chatId, $"❌ {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send error message to chatId: {ChatId}", chatId);
            }
        }

        private async Task HandleOrderAmountInputAsync(long chatId, long telegramId, string amountText)
        {
            if (!_conversations.TryGet(telegramId, out var conversation))
            {
                await _messenger.SendAsync(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
            {
                await _messenger.SendAsync(chatId, "لطفاً مقدار معتبر وارد کنید.");
                return;
            }

            var orderState = conversation;

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

                // The same confirmation templates as the ordinary order path. A second
                // template carrying the same information would be a second place to
                // maintain, and the customer expects the confirmation to always look the
                // same. No per-mesghal override here: the quote is the source of the
                // price, so the derived figure is the authoritative one.
                var quoteMessage = OrderConfirmationMessage.Build(
                    orderState.Asset, orderState.OrderSide, orderState.Amount, pricePerGram);

                await _messenger.SendAsync(chatId, quoteMessage,
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

                await _messenger.SendAsync(chatId, pricePrompt,
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
                    await _messenger.SendAsync(chatId, "خطا در دریافت بهترین قیمت بازار. لطفاً دوباره تلاش کنید.");
                    _conversations.Clear(telegramId);
                    return;
                }

                await HandleOrderPriceInputAsync(chatId, telegramId, orderState.Price.ToString());
            }
        }

        private async Task HandleOrderPriceInputAsync(long chatId, long telegramId, string priceStr)
        {
            if (!_conversations.TryGet(telegramId, out var conversation))
            {
                await _messenger.SendAsync(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            if (!decimal.TryParse(priceStr, out var price) || price <= 0)
            {
                await _messenger.SendAsync(chatId, "لطفاً قیمت معتبر وارد کنید.");
                return;
            }

            var orderState = conversation;
            orderState.Price = price;
            orderState.State = "";

            // The customer types the price of one mesghal; gold is stored per gram. The
            // number they typed is kept so the confirmation can show it back to them,
            // otherwise they see a figure that is not the one they entered.
            var enteredPricePerMesghal = price;
            var isGold = orderState.Asset == CurrenciesConstant.MAUA_IRT;

            if (isGold)
            {
                // Was a literal 4.3318 here, duplicating the constant. Two copies of a
                // conversion factor is one copy too many: changing one and not the other
                // would misprice every gold order with nothing failing.
                orderState.Price /= CurrenciesConstant.GramsPerMesghal;
            }

            var validateCreditAndBalance =
                await _walletApi.ValidateCreditAndBalanceAsync(orderState.UserId, orderState.Asset, orderState.Amount, orderState.Price);

            var hasSufficientBalance = orderState.OrderSide == OrderSide.Buy
                ? validateCreditAndBalance.HasSufficientCreditAndBalanceQuote : validateCreditAndBalance.HasSufficientCreditAndBalanceBase;

            var isAdmin = await GetUserRoleAsync(chatId) == TallaEgg.Core.Enums.User.UserRole.Admin;

            if (!isAdmin)    
            if (!validateCreditAndBalance.Success || !hasSufficientBalance)
            {
                var backBtn = new KeyboardButton(BotBtns.BtnBack);
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgInsufficientBalance, validateCreditAndBalance.Message),
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
                            new KeyboardButton[] { backBtn }
                    })
                    {
                        ResizeKeyboard = true
                    });
                _conversations.Clear(telegramId);
                return;
            }

            // The per-mesghal figure shown is the customer's own input, not one derived
            // back from the stored per-gram price: the division does not round-trip, and a
            // confirmation quoting a slightly different number than they typed reads as
            // the bot having mis-recorded the order.
            var confirmationMessage = OrderConfirmationMessage.Build(
                orderState.Asset,
                orderState.OrderSide,
                orderState.Amount,
                orderState.Price,
                displayPricePerMesghal: enteredPricePerMesghal);

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnConfirm, InlineCallBackData.confirm_order),
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnCancel, InlineCallBackData.cancel_order)
                }
            });

            //orderState.IsConfirmed = true;
            await _messenger.SendAsync(chatId, confirmationMessage, replyMarkup: keyboard);
        }

        private async Task HandleOrderConfirmationAsync(long chatId, long telegramId)
        {
            if (!_conversations.TryGet(telegramId, out var conversation))
            {
                await _messenger.SendAsync(chatId, "خطا در پردازش سفارش. لطفاً دوباره تلاش کنید.");
                return;
            }

            var orderState = conversation;

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
                    await _messenger.SendAsync(chatId, BotMsgs.MsgOrderSuccess,
                        replyMarkup: new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton[] { backBtn }
                        })
                        {
                            ResizeKeyboard = true
                        });

                    // در مسیر مظنه‌ای معامله همان لحظه انجام شده، پس نتیجه‌اش بلافاصله
                    // اعلام می‌شود. در مسیر دفتر سفارش، سفارش ممکن است ساعت‌ها منتظر بماند
                    // و هنوز معامله‌ای وجود ندارد که گزارش شود — به همین دلیل این پیام
                    // فقط وقتی فرستاده می‌شود که واقعاً معامله‌ای انجام شده باشد، نه صرفاً
                    // چون سفارش ثبت شد.
                    if (quote is not null)
                        await SendTradeExecutedAsync(chatId, orderState);
                }
                else
                {
                    await _messenger.SendAsync(chatId,
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
                await _messenger.SendAsync(chatId, $"خطا در ثبت سفارش: {ex.Message}");
            }
            finally
            {
                _conversations.Clear(telegramId);
            }
        }

        /// <summary>
        /// Reports a trade that has actually executed.
        ///
        /// Uses the same values the customer was shown in the confirmation, so the number
        /// they approved and the number reported back are necessarily the same. Re-reading
        /// the trade from the server would not give that guarantee and would add a network
        /// call.
        ///
        /// A failure to send must break nothing: the trade is executed and settled, and it
        /// is visible under "trade history".
        /// </summary>
        private async Task SendTradeExecutedAsync(long chatId, OrderState orderState)
        {
            try
            {
                await _messenger.SendAsync(chatId, TradeExecutedMessage.Build(
                    orderState.Asset, orderState.OrderSide, orderState.Amount, orderState.Price));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send the trade-executed message to chat {ChatId}.", chatId);
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
            await _messenger.SendAsync(user.Id, message);
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
                        await _messenger.SendAsync(user.TelegramId, message);

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