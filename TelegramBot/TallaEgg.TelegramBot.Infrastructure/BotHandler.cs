using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
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

namespace TallaEgg.TelegramBot.Infrastructure
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

        /// <summary>
        /// Telegram ids that are operators no matter what the database says. See
        /// <see cref="Infrastructure.Options.BotSettingsOptions.OwnerTelegramIds"/> for why
        /// this cannot be a stored role.
        /// </summary>
        private readonly IReadOnlySet<long> _ownerTelegramIds;

        public BotHandler(ILogger<BotHandler> logger,
                         ITelegramBotClient botClient, IBotMessenger messenger,
                         IConversationStore conversations,
                         IOrderApiClient orderApi, IUsersApiClient usersApi,
                         IAffiliateApiClient affiliateApi, IWalletApiClient walletApi,
                         ITelegramLogger telegramLogger, IVersionService versionService,
                         bool requireReferralCode = false,
                         string defaultReferralCode = BootstrapConstant.RootInvitationCode,
                         IEnumerable<long>? ownerTelegramIds = null)
        {
            _ownerTelegramIds = ownerTelegramIds?.ToHashSet() ?? [];

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
            // No local catch here (issue #99) — this used to swallow every exception with a
            // log line and total silence to the user, which TelegramBotHostedService.
            // HandleUpdateAsync's own catch can never see or recover from because nothing
            // propagates past this point. Letting it bubble means the caller's catch — which
            // does log to disk and reply with a fallback message — is the one and only place
            // an unhandled exception from a customer's message is handled, for every message,
            // not just the ones that happen not to hit this now-removed try.
            var chatId = message.Chat.Id;
            var telegramId = message.From?.Id ?? 0;
            await _telegramLogger.LogAsync<Message>($"✔➕ new message:",message);


            // Absent on a contact, photo or sticker message, all of which reach this handler.
            if (message.Text is not null)
            {
                message.Text = TallaEgg.Core.Utilties.Utils.ConvertPersianDigitsToEnglish(message.Text);
            }

            // Check if user exists
            var user = await _usersApi.GetUserAsync(telegramId);

            if (user == null)
            {
                // /start is the registration command, so telling somebody to send it while
                // they are sending it is noise — and it arrived as the reply to their very
                // first message, which reads like a rejection. Anything else still gets the
                // prompt.
                if (!(message.Text ?? string.Empty).StartsWith("/start"))
                    await _messenger.SendAsync(chatId, BotMsgs.MsgAccountNotFound);

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

            // A configured owner is exempt from the approval gate, and this is the whole
            // reason the exemption exists: a new account is created Pending, approval
            // arrives only as a callback from an administrator of a hard-coded Telegram
            // group, and the operator commands live inside the else-branch below. On an
            // empty database that is a closed loop — the one person who is supposed to
            // appoint the first administrator sits behind an approval only an existing
            // administrator can give.
            //
            // Ownership already comes from configuration, which is to say from whoever
            // controls the deployment. Requiring a second, in-product approval on top of
            // that adds no protection; it only makes the product impossible to start.
            if (user.Status != TallaEgg.Core.Enums.User.UserStatus.Approved
                && !_ownerTelegramIds.Contains(telegramId))
            {
                await _messenger.SendAsync(
                     chatId,
                     string.Format(BotMsgs.MsgAccountNotApproved, user.FirstName).AutoRtl()
                 );
            }
            else
            {
                // Operator, not Telegram group administrator: who may run admin commands is the
                // product's own answer, not a property of one chat. Splitting an accountant role
                // out of it is an open design question, not a decision made here.
                if (IsOperator(user))
                {
                    // Check for admin commands first
                    bool isAdminCmd = await HandleAdminCommandsAsync(chatId, telegramId, message, user);
                    if (isAdminCmd) return;
                }

                await HandleMainMenuAsync(chatId, telegramId, message, user.Id);
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
                await _messenger.SendContactKeyboardAsync(chatId);
            }
            else
            {
                await _messenger.SendAsync(chatId, $"خطا در ثبت‌نام: {regMessage}");
            }
        }

        private async Task HandlePhoneNumberRequestAsync(long chatId, long telegramId, Message message)
        {
            var phoneNumber = message.Contact?.PhoneNumber;
            if (phoneNumber != null)
            {
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

                    // A configured owner approves themselves, because there is nobody else to
                    // do it. Their authority already comes from the configuration file; asking
                    // them to also tick a box about themselves is a formality that on an empty
                    // system nobody can perform. Approving the row as well as exempting them at
                    // the gate keeps the stored state honest — they show as Approved in the user
                    // list, rather than looking Pending forever while behaving otherwise.
                    if (_ownerTelegramIds.Contains(telegramId))
                    {
                        await _usersApi.UpdateUserStatusAsync(telegramId, UserStatus.Approved);

                        // ...and given the Admin role, not merely treated as one.
                        //
                        // Being an operator by configuration is enough to reach the commands,
                        // but it is not enough to be the shop: the account that publishes a
                        // quote becomes the counterparty of every fill against it, and only an
                        // Admin is exempt from the balance check that would otherwise refuse
                        // those trades. Leaving the stored role at RegularUser would give the
                        // owner a menu they cannot actually use.
                        //
                        // This is what removes the last manual step from a first deployment:
                        // configure one Telegram id, register, and the shop exists.
                        if (response.Data is not null && response.Data.Role != UserRole.Admin)
                        {
                            var (promoted, promotionMessage) =
                                await _usersApi.UpdateRoleAsync(response.Data.Id, UserRole.Admin);

                            if (promoted)
                                _logger.LogInformation(
                                    "Configured owner {UserId} was granted the Admin role on registration.",
                                    response.Data.Id);
                            else
                                _logger.LogError(
                                    "Configured owner {UserId} could not be granted the Admin role: {Message}",
                                    response.Data.Id, promotionMessage);
                        }

                        return;
                    }

                    // Who gets asked to approve a new registration must be the same "who is an
                    // operator" the product already uses everywhere else (IsOperator, "ت"/"ر").
                    // It used to be "whoever administers one hard-coded Telegram group" instead —
                    // unrelated to that answer, and it throws outright when the bot is not a
                    // member of that group, which silently drops the notification for everyone.
                    var adminIds = (await _usersApi.GetOperatorTelegramIdsAsync())
                        .Union(_ownerTelegramIds)
                        .ToList();
                    if (response.Data is not null)
                    {
                        await _messenger.SendApproveOrRejectUserToAdminsKeyboard(adminIds, response.Data);
                    }
                    else
                    {
                        // A success with no user in it is a contract violation by the Users API,
                        // not something the customer can act on, so it is logged rather than shown.
                        _logger.LogError(
                            "Users API reported a successful registration for {TelegramId} but returned no user.",
                            telegramId);
                    }
                }
                else
                {
                    await _messenger.SendAsync(chatId, response.Message ?? BotMsgs.MsgUnexpectedError);
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
                case BotBtns.BtnBack:
                    // Checked ahead of the default branch's order-flow routing on purpose: a
                    // customer stuck in "waiting_for_amount"/"waiting_for_price" (no quote, a
                    // typo, changed their mind) previously had this typed as an invalid amount
                    // or price instead of as a way out, because default's state check ran
                    // before this text was ever compared against a button label.
                    _conversations.Clear(telegramId);
                    await ShowMainMenuAsync(chatId);
                    break;

                case BotBtns.BtnSpotSubmitPrice:
                    // For an operator this button is a status check, not the start of a
                    // conversation: quotes are published with the "buyPrice-sellPrice"
                    // command (or the auto-quote publisher), never through this flow, so
                    // walking an admin through symbol/price/quantity prompts here answered a
                    // question nobody was asking. Showing the latest published quote is the
                    // thing an admin actually wants from this button.
                    if (await IsOperatorAsync(chatId))
                    {
                        await ShowLatestQuoteAsync(chatId);
                        break;
                    }

                    // A non-operator reaching this (a replayed or forwarded button label)
                    // still gets the ordinary quote flow, same as BtnSpotMarket.
                    _conversations.GetOrStart(telegramId, userId).OrderType = OrderType.Limit;
                    await ShowSymbolsAsync(chatId, telegramId);
                    break;

                case BotBtns.BtnSpotCreateOrder:
                case BotBtns.BtnSpotMarket:

                    OrderType orderType = msgText == BotBtns.BtnSpotCreateOrder
                        ? OrderType.Limit
                        : OrderType.Market;

                    // Starts the conversation if the customer has none: this is the first
                    // step of the order flow, so there is nothing yet to find.
                    _conversations.GetOrStart(telegramId, userId).OrderType = orderType;

                    await ShowSymbolsAsync(chatId, telegramId);

                    break;

                case BotBtns.BtnAccounting:
                    await HandleAccountingMenuAsync(chatId);
                    break;
                case BotBtns.BtnQuoteHistory:
                    await ShowQuoteHistory(chatId);
                    break;
                case BotBtns.BtnTradeHistory:
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
            // Telegram omits Message for callbacks raised from an inline message, and for any
            // message older than 48 hours. Every branch below edits or deletes that message, and
            // the chat id used to fall back to 0, which no send can reach — so none of them could
            // do anything useful anyway. Answering the callback stops the client's spinner and
            // says why, instead of an unhandled NullReferenceException on the first old button
            // somebody taps.
            if (callbackQuery.Message is null)
            {
                await _messenger.AnswerCallbackAsync(callbackQuery.Id, BotMsgs.MsgCallbackMessageGone);
                return;
            }

            var message = callbackQuery.Message;
            var chatId = message.Chat.Id;
            var telegramId = callbackQuery.From?.Id ?? 0;
            var data = callbackQuery.Data ?? "";

            // Answering a held quote carries the proposal id after a colon, so it cannot be matched
            // by the constant switch below (issue #158). Handled first, and only for an admin: the
            // buttons are only ever sent to admins, but a callback is just a string and anyone who
            // has seen one could send it back.
            if (data.StartsWith(InlineCallBackData.approve_quote, StringComparison.Ordinal) ||
                data.StartsWith(InlineCallBackData.reject_quote, StringComparison.Ordinal))
            {
                var responder = await _usersApi.GetUserAsync(telegramId);

                if (responder is null || !IsOperator(responder))
                {
                    await _messenger.AnswerCallbackAsync(callbackQuery.Id, BotMsgs.MsgCallbackMessageGone);
                    return;
                }

                await _messenger.AnswerCallbackAsync(callbackQuery.Id, "");
                await HandlePendingQuoteDecisionAsync(chatId, responder.Id, data);
                return;
            }

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

                    // A bare ReplyKeyboardRemove left the customer typing into a text-only
                    // prompt with no way out except knowing to type "🔙 بازگشت" from memory —
                    // the BtnBack case in HandleMainMenuAsync's switch handles the tap/text
                    // itself, but only if there is a button to tap.
                    await _messenger.SendAsync(chatId,
                                                 $"لطفاً مقدار را وارد کنید.",
                                                 replyMarkup: new ReplyKeyboardMarkup(new[]
                                                 {
                                                     new KeyboardButton[] { new KeyboardButton(BotBtns.BtnBack) }
                                                 })
                                                 {
                                                     ResizeKeyboard = true
                                                 });

                    break;

                case InlineCallBackData.confirm_order:
                    await HandleOrderConfirmationAsync(chatId, telegramId);
                    break;

                case InlineCallBackData.cancel_order:
                    _conversations.Clear(telegramId);
                    await ShowMainMenuAsync(chatId);
                    break;

                // Both top-up paths reach the same message: there is no payment gateway today and
                // accounts are topped up by the gold shop.
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

                        // Both prices come from the same published Quote (issue #48) — always
                        // both present or both absent — so either one missing means there is no
                        // quote for this symbol at all, not merely a one-sided book.
                        // The published quote, or null when there is none. Held as the value
                        // rather than a bool so the branch below reads it without having to assert
                        // it is there.
                        var quote = apiResponse is { Success: true, Data: not null }
                            && apiResponse.Data.BestBidPrice.HasValue
                            && apiResponse.Data.BestAskPrice.HasValue
                                ? apiResponse.Data
                                : null;

                        if (apiResponse != null && apiResponse.Success)
                        {
                            await _messenger.DeleteAsync(chatId, message.Id);

                            await _messenger.SendAsync(chatId, BestPricesMessage.Build(
                                apiResponse.Data?.BestBidPrice, apiResponse.Data?.BestAskPrice, asset));

                            if (quote is not null)
                            {
                                // Stored for the market-order path (HandleOrderTypeSelectionAsync),
                                // which feeds this straight into HandleOrderPriceInputAsync — the same
                                // place a manually-typed limit price lands. That method only divides a
                                // gold price back down from per-mesghal to per-gram, so gold's stored
                                // value must already be per-mesghal here; every other symbol has no
                                // such internal/display split and is stored as-is.
                                var isGold = asset == CurrenciesConstant.MAUA_IRT;
                                assetConversation.BestBidPrice = isGold
                                    ? quote.BestBidPrice * CurrenciesConstant.GramsPerMesghal
                                    : quote.BestBidPrice;
                                assetConversation.BestAskPrice = isGold
                                    ? quote.BestAskPrice * CurrenciesConstant.GramsPerMesghal
                                    : quote.BestAskPrice;
                            }
                        }

                        // Only the dealer/market path is a dead end without a quote — it never
                        // asks for a price, so with no quote it has nothing to trade on. The
                        // order-book (limit) path is unaffected: it asks the customer for a
                        // price itself and never depended on GetBestPricesAsync succeeding.
                        if (quote is not null || assetConversation.OrderType != OrderType.Market)
                        {
                            await _messenger.SendSpotSideMenuKeyboard(chatId);
                        }
                        else
                        {
                            _conversations.Clear(telegramId);
                            await _messenger.SendAsync(chatId, BotMsgs.MsgNoQuoteForSymbol);
                            await ShowMainMenuAsync(chatId);
                        }
                    }
                    // Both branches activate or bar an account, and neither checked who was
                    // asking. Callback data is not a secret — it is client-side text that any
                    // Telegram client can send back — so "approve_<id>" was effectively an open
                    // endpoint for approving accounts. It went unnoticed because the buttons are
                    // only ever delivered to group administrators, but delivery is not a
                    // permission check.
                    else if (data.StartsWith("approve_") || data.StartsWith("reject_"))
                    {
                        if (!await IsOperatorAsync(telegramId))
                        {
                            await _messenger.AnswerCallbackAsync(callbackQuery.Id, BotMsgs.MsgNotAuthorized);
                            return;
                        }

                        var approving = data.StartsWith("approve_");
                        var prefixLength = (approving ? "approve_" : "reject_").Length;
                        var targetTelegramId = long.Parse(data[prefixLength..]);

                        if (approving)
                            await ApproveUser(targetTelegramId, telegramId, message);
                        else
                            await RejectUser(targetTelegramId, telegramId, message);
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

                            // Edit the previous message.
                            await _messenger.EditTextAsync(
                                chatId: message.Chat.Id,
                                messageId: message.MessageId,
                                text: text,
                                replyMarkup: OrderListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // Dismiss the "please wait" spinner on the button.
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

                            // uid is the user viewing the list; it decides whether each trade was a
                            // buy or a sell from their point of view.
                            var pagerIsAdmin = await IsOperatorAsync(chatId);
                            var pagerPhones = await ResolveCounterpartyPhonesAsync(page.Data, uid, pagerIsAdmin);

                            var text = await TradeListHandler.BuildTradesListAsync(page.Data!, pageNum, uid, pagerPhones);

                            // Edit the previous message.
                            await _messenger.EditTextAsync(
                                chatId: message.Chat.Id,
                                messageId: message.MessageId,
                                text: text,
                                replyMarkup: TradeListHandler.BuildPagingKeyboard(page.Data!, pageNum, uid)
                            );

                            // Dismiss the "please wait" spinner on the button.
                            await _messenger.AnswerCallbackAsync(callbackQuery.Id);
                        }
                    }
                    else if (data.StartsWith(QuoteHistoryHandler.CallbackPrefix))
                    {
                        // quotes_{BASE}/{QUOTE}_{page} — the symbol contains a '/', not a '_',
                        // so splitting on the last underscore keeps the symbol intact.
                        var payload = data[QuoteHistoryHandler.CallbackPrefix.Length..];
                        var split = payload.LastIndexOf('_');

                        if (split > 0 && int.TryParse(payload[(split + 1)..], out var quotePage))
                        {
                            var symbol = payload[..split];
                            var isAdmin = await IsOperatorAsync(chatId);

                            if (!isAdmin)
                            {
                                await _messenger.AnswerCallbackAsync(callbackQuery.Id);
                                return;
                            }

                            var quotePageResult = await _orderApi.GetQuoteHistoryAsync(symbol, quotePage, pageSize: 5);

                            await _messenger.EditTextAsync(
                                chatId: message.Chat.Id,
                                messageId: message.MessageId,
                                text: QuoteHistoryHandler.BuildQuoteHistoryAsync(quotePageResult, quotePage, isAdmin, symbol),
                                replyMarkup: QuoteHistoryHandler.BuildPagingKeyboard(quotePageResult, quotePage, symbol));

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
                                
                                // Delete the message or update it.
                                await _messenger.EditTextAsync(
                                    chatId: message.Chat.Id,
                                    messageId: message.MessageId,
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

                            // Load the user data for the new page.
                            var page = await _usersApi.GetUsersAsync(newPage, 5, query); // (pageNumber, pageSize, query)

                            var text = await UserListHandler.BuildUsersListAsync(page.Data!, newPage, query);

                            // Edit the previous message.
                            await _messenger.EditTextAsync(
                                chatId: message.Chat.Id,
                                messageId: message.MessageId,
                                text: text,
                                parseMode: ParseMode.MarkdownV2,
                                replyMarkup: UserListHandler.BuildPagingKeyboard(page.Data!, newPage, query)
                            );

                            // Dismiss the "please wait" spinner on the button.
                            await _messenger.AnswerCallbackAsync(callbackQuery.Id);
                        }
                    }
                    break;
            }

            await _messenger.AnswerCallbackAsync(callbackQuery.Id);
        }
        /// <summary>
        /// </summary>
        /// <param name="chatId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<UserRole> GetUserRoleAsync(long chatId)
        {
            var user = await _usersApi.GetUserAsync(chatId);

            if (user == null)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAccountNotFound);
                throw new Exception("User not found");
            }

            return user.Role;
        }

        /// <summary>
        /// Whether this person may use the administrative side of the bot.
        ///
        /// <para>
        /// Three things count, and the call sites used to check only the first:
        /// </para>
        /// <list type="bullet">
        ///   <item><description><see cref="UserRole.Admin"/> — the shop operator.</description></item>
        ///   <item><description><see cref="UserRole.SuperAdmin"/> — strictly more privileged, yet
        ///   <c>role == Admin</c> excluded it. Granting someone the higher of the two roles took
        ///   their admin menu away, which is exactly backwards, and now that roles can be changed
        ///   from inside the bot it is a mistake anyone could make in one message.</description></item>
        ///   <item><description>A configured owner — see
        ///   <see cref="Infrastructure.Options.BotSettingsOptions.OwnerTelegramIds"/>. Without this
        ///   an empty database has no operator and no way to appoint one.</description></item>
        /// </list>
        /// </summary>
        private bool IsOperator(UserDto user) =>
            user.Role is UserRole.Admin or UserRole.SuperAdmin
            || _ownerTelegramIds.Contains(user.TelegramId);

        /// <summary>
        /// The same question asked when only the chat is at hand. In a private chat — the only
        /// place this bot is used — the chat id and the Telegram user id are the same value,
        /// which is the assumption every other lookup here already makes.
        /// </summary>
        private async Task<bool> IsOperatorAsync(long chatId)
        {
            if (_ownerTelegramIds.Contains(chatId))
                return true;

            var role = await GetUserRoleAsync(chatId);
            return role is UserRole.Admin or UserRole.SuperAdmin;
        }

        private async Task ShowMainMenuAsync(long chatId)
        {
            //bool isAdmin = await IsTelegramAdmin(user);
            //isAdmin = true; // for test
            ////if (isAdmin)

            if (await IsOperatorAsync(chatId))
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
            if (await IsOperatorAsync(chatId))
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
            // IsOperatorAsync, not a raw role check: MsgAdminMainHelp describes the operator's
            // own menu (which SuperAdmin and a configured owner also see via IsOperator), not
            // only the literal Admin role.
            var isOperator = await IsOperatorAsync(chatId);

            var helpText = isOperator ? BotMsgs.MsgAdminMainHelp : BotMsgs.MsgUserHelp;

            if (isOperator)
            {
                helpText += BotMsgs.MsgAdminHelp + "\n\n";
            }

            helpText += BotMsgs.MsgSupportFooter;

            await _messenger.SendAsync(chatId, helpText);
        }
        /// <summary>
        /// The prices the shop has published, newest first.
        ///
        /// Replaced order history in the accounting menu. An order in the dealer model is
        /// created and consumed inside a single fill, so a customer's order list only ever
        /// held completed rows — it looked like information and was not.
        /// </summary>
        /// <summary>
        /// The single most recent quote for every active Dealer-mode symbol, active or not —
        /// what "💹 اعلام مظنه" shows an operator. One message per symbol, since each comes from
        /// its own <see cref="_orderApi.GetQuoteHistoryAsync"/> page. Reuses
        /// <see cref="QuoteHistoryHandler"/>'s renderer with a one-item page rather than a
        /// second formatter for what is the same data at a different page size.
        /// </summary>
        private async Task ShowLatestQuoteAsync(long chatId)
        {
            foreach (var symbol in await _orderApi.GetActiveSymbolsAsync())
            {
                var page = await _orderApi.GetQuoteHistoryAsync(symbol, pageNumber: 1, pageSize: 1);

                await _messenger.SendAsync(chatId, QuoteHistoryHandler.BuildQuoteHistoryAsync(page, currentPage: 1, isAdmin: true, symbol));
            }
        }

        private async Task ShowQuoteHistory(long chatId)
        {
            var isAdmin = await IsOperatorAsync(chatId);

            // The button is only on the admin keyboard, but a keyboard label is just text a
            // customer can type or replay from an old message. The check belongs here, where
            // the data is read, not only in the menu that offers it.
            if (!isAdmin)
            {
                await ShowMainMenuAsync(chatId);
                return;
            }

            // One message (with its own paging keyboard) per active symbol — paging then
            // continues to work exactly as it does today, since each keyboard's callback data
            // already carries the symbol it pages within (QuoteHistoryHandler.CallbackPrefix).
            foreach (var symbol in await _orderApi.GetActiveSymbolsAsync())
            {
                var page = await _orderApi.GetQuoteHistoryAsync(symbol, pageNumber: 1, pageSize: 5);

                await _messenger.SendAsync(
                    chatId,
                    QuoteHistoryHandler.BuildQuoteHistoryAsync(page, 1, isAdmin, symbol),
                    replyMarkup: QuoteHistoryHandler.BuildPagingKeyboard(page, 1, symbol));
            }
        }

        /// <summary>
        /// Phone numbers for the other side of each trade on a page, or null when the viewer
        /// is not an admin.
        ///
        /// Resolves the distinct ids only. A page holds at most five trades and the shop
        /// trades with a handful of customers, so this is normally one or two lookups rather
        /// than one per row.
        ///
        /// A lookup that fails is skipped rather than propagated: a missing phone number
        /// should cost the admin one line of a list, not the whole list.
        /// </summary>
        private async Task<IReadOnlyDictionary<Guid, string>?> ResolveCounterpartyPhonesAsync(
            PagedResult<TradeHistoryDto>? page, Guid viewerUserId, bool isAdmin)
        {
            if (!isAdmin || page is null) return null;

            var counterpartyIds = page.Items
                .Select(t => t.BuyerUserId == viewerUserId ? t.SellerUserId : t.BuyerUserId)
                .Where(id => id != viewerUserId)
                .Distinct()
                .ToList();

            var phones = new Dictionary<Guid, string>();

            foreach (var id in counterpartyIds)
            {
                try
                {
                    var user = await _usersApi.GetUserByIdAsync(id);
                    if (!string.IsNullOrWhiteSpace(user?.PhoneNumber))
                        phones[id] = user!.PhoneNumber!;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not resolve the phone number for user {UserId}.", id);
                }
            }

            return phones;
        }

        /// <summary>
        /// One customer's trades, for an admin who looked them up by phone number.
        ///
        /// Built from the customer's point of view, so buy and sell read the way that customer
        /// experienced them. No counterparty column: every row's counterparty is the shop, and
        /// repeating the admin's own number on each line would be noise.
        /// </summary>
        private async Task ShowCustomerTradeHistoryAsync(long chatId, Guid customerUserId, string phone)
        {
            var page = await _orderApi.GetUserTradesAsync(customerUserId, pageNumber: 1, pageSize: 5);

            if (!page.Success)
            {
                await _messenger.SendAsync(chatId, "خواندن معاملات این مشتری انجام نشد.");
                return;
            }

            var header = $"👤 معاملات مشتری {PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone))}\n\n";
            var text = await TradeListHandler.BuildTradesListAsync(page.Data!, 1, customerUserId);

            await _messenger.SendAsync(
                chatId,
                header + text,
                replyMarkup: TradeListHandler.BuildPagingKeyboard(page.Data!, 1, customerUserId));
        }

        private async Task ShowTradeHistory(long chatId, Guid userId)
        {
            var page = await _orderApi.GetUserTradesAsync(userId, pageNumber: 1, pageSize: 5);
            if (page.Success)
            {
                var isAdmin = await IsOperatorAsync(chatId);
                var phones = await ResolveCounterpartyPhonesAsync(page.Data, userId, isAdmin);

                var text = await TradeListHandler.BuildTradesListAsync(page.Data!, 1, userId, phones);

                await _messenger.SendAsync(
                    chatId: chatId,
                    text: text,
                    replyMarkup: TradeListHandler.BuildPagingKeyboard(page.Data!, 1, userId)
                );
            }
        }

        private async Task ShowWalletsBalance(long chatId, Guid userId)
        {
            var res = await _walletApi.GetUserWalletsBalanceAsync(userId);
            if (res.Success)
            {
                if (res.Data?.Any() == true)
                {
                    // A failed positions fetch must not block the balance screen the customer
                    // actually asked for -- WalletBalanceMessage treats a null positions
                    // argument as "no P&L data available" and still renders the balances.
                    var positionsRes = await _orderApi.GetPositionsAsync(userId);
                    var positions = positionsRes.Success ? positionsRes.Data : null;

                    await _messenger.SendAsync(chatId, WalletBalanceMessage.Build(res.Data, positions));

                }
                else
                {
                    await _messenger.SendAsync(chatId, BotMsgs.MsgNoWallet);

                }
            }
            else
            {

                await _messenger.SendAsync(chatId, res.Message ?? BotMsgs.MsgUnexpectedError);
            }


        }

        /// <summary>
        /// Shows the active trading symbols after the user has chosen an order type.
        /// Display active trading symbols to user after order type selection
        /// </summary>
        /// <param name="chatId">Telegram chat id.</param>
        /// <param name="telegramId">Telegram user id.</param>
        /// <returns>Whether the message was sent.</returns>
        private async Task<bool> ShowSymbolsAsync(long chatId, long telegramId)
        {
            try
            {
                // Check the user has conversation state.
                if (!_conversations.TryGet(telegramId, out var conversation))
                {
                    _logger.LogWarning("User order state not found for telegramId: {TelegramId}", telegramId);
                    await SendErrorMessageAsync(chatId, "خطا در پردازش سفارش. لطفاً از منوی اصلی دوباره شروع کنید.");
                    return false;
                }

                // Fetch the active trading pairs.
                var activeTradingPairs = await GetActiveTradingPairsAsync();

                if (!activeTradingPairs.Any())
                {
                    _logger.LogError("No active trading pairs found");
                    await SendErrorMessageAsync(chatId, "در حال حاضر نمادی برای معامله فعال نیست. لطفاً بعداً تلاش کنید.");
                    return false;
                }

                // Build the symbol buttons.
                var symbolButtons = CreateSymbolButtons(activeTradingPairs);

                // Add the back button.
                symbolButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(BotBtns.BtnBack, InlineCallBackData.BackToMain)
                });

                var keyboard = new InlineKeyboardMarkup(symbolButtons.ToArray());

                // Send the message, handling failures.
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
        /// The symbols currently tradable. Whether a symbol is enabled lives in the Orders service's
        /// database rather than here, because an admin bot command has to be able to change it
        /// without a rebuild or a restart. Each symbol's metadata, its Persian name and so on, still
        /// comes from <see cref="CurrenciesConstant"/>.
        /// </summary>
        /// <returns>The active trading pairs.</returns>
        private async Task<List<TradingPairInfo>> GetActiveTradingPairsAsync()
        {
            try
            {
                var activeSymbols = await _orderApi.GetActiveSymbolsAsync();

                var activePairs = activeSymbols
                    .Select(CurrenciesConstant.GetTradingPairInfo)
                    .Where(pair => pair is not null &&
                                  !string.IsNullOrWhiteSpace(pair.Symbol) &&
                                  !string.IsNullOrWhiteSpace(pair.PersianName))
                    .Cast<TradingPairInfo>()
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
        /// Builds the symbol buttons, subject to a count limit.
        /// </summary>
        /// <param name="tradingPairs">The trading pairs.</param>
        /// <returns>The inline keyboard buttons.</returns>
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
                        // Extra validation per pair.
                        if (string.IsNullOrWhiteSpace(pair.Symbol) || string.IsNullOrWhiteSpace(pair.PersianName))
                        {
                            _logger.LogWarning("Invalid trading pair data: Symbol={Symbol}, PersianName={PersianName}",
                                pair.Symbol, pair.PersianName);
                            continue;
                        }

                        var callbackData = $"{InlineCallBackData.AssetPrefix}_{pair.Symbol}";

                        // Check the callback data length; Telegram's limit is 64 characters.
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

                    // Symbols past this limit are simply not offered. With a handful of
                    // symbols nobody notices; the day the list grows, a customer cannot reach
                    // the ones that fell off, and there is no sign in the bot that they exist.
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
        /// Sends a standard error message.
        /// </summary>
        /// <param name="chatId">Chat id.</param>
        /// <param name="errorMessage">The error message.</param>
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

            // In dealer mode the customer is never asked for a price (issue #48): the price is the
            // admin's published quote. That removes the whole mesghal/gram ambiguity from the
            // customer's flow — they only give a quantity.
            var activeQuote = await _orderApi.GetActiveQuoteAsync(orderState.Asset);

            if (activeQuote is not null)
            {
                // The quote price is stored per gram and converted to mesghal for display, because
                // that is the unit the user knows prices in.
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

                // For melted gold the prompt asks for the price of one mesghal, naming the unit
                // explicitly.
                var pricePrompt = orderState.Asset == CurrenciesConstant.MAUA_IRT
                    ? BotMsgs.MsgEnterPriceGold
                    : string.Format(BotMsgs.MsgEnterPrice, PersianFormat.Symbol(orderState.Asset));

                await _messenger.SendAsync(chatId, pricePrompt,
                 replyMarkup: new ReplyKeyboardMarkup(new[]
                 {
                     new KeyboardButton[] { new KeyboardButton(BotBtns.BtnBack) }
                 })
                 {
                     ResizeKeyboard = true
                 });
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

            var isAdmin = await IsOperatorAsync(chatId);

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
                // In dealer mode the customer fills the published quote and sends no price
                // (issue #48). The server reads the price from the quote, so there is no way for
                // what the customer saw and what is traded to differ.
                //
                // With no published quote we fall back to the ordinary order path — the order-book
                // behaviour, which other symbols need and which this symbol will need if it moves to
                // OrderBook mode.
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

                    // On the quote path the trade executed in the same instant, so its outcome is
                    // announced immediately. On the order-book path the order may rest for hours
                    // with no trade yet to report — which is why this message is sent only when a
                    // trade genuinely executed, not merely because an order was placed.
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
            finally
            {
                // Business failures (insufficient balance, no quote, etc.) already come back
                // as (orderSuccess: false, orderMessage) above and are shown to the customer
                // there — this method only ever throws for something genuinely unexpected, and
                // the catch that used to sit here sent the customer ex.Message verbatim, with
                // no logging anywhere (issue #99). Letting it bubble reaches
                // TelegramBotHostedService.HandleUpdateAsync's catch, which does both. The
                // conversation must still be cleared on that path — same as any other exit —
                // or the next order silently inherits this one's half-filled state.
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
        /// Builds the startup announcement: an update message with a changelog when the version has
        /// genuinely changed, otherwise a "back online" message.
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

                // Wait 30 seconds so the Users service is up; without it this fails.
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

                        // Stay under Telegram's rate limit.
                        await Task.Delay(50);
                    }
                    catch (Exception ex)
                    {
                        // Always log this.
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