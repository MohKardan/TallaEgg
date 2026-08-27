using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.AllServices.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A customer's whole ordering conversation, driven end to end (issue #65).
///
/// This is what slices 2 and 3 were for. <c>BotHandler</c> is the busiest class in the
/// product and the only part of the money path with no automated coverage, because
/// three things made it untestable: the send methods were extension methods and could not
/// be substituted, conversation state was a private dictionary, and the constructor started
/// a background thread and messaged every user. With a messenger interface, an injected
/// store, and startup work moved out of the constructor, the handler can now simply be
/// built and talked to.
///
/// The flow under test is the dealer model (issue #48): the admin publishes a quote and the
/// customer trades on it. Its defining property — that the customer is <b>never asked for a
/// price</b> — had no test at all until now, and it is the property that removes the whole
/// gram/mesghal ambiguity from the customer's side.
/// </summary>
public class BotConversationFlowTests
{
    private const long ChatId = 555_000;
    private const long TelegramId = 555_000;
    private static readonly Guid CustomerId = Guid.NewGuid();

    private const string Gold = CurrenciesConstant.MAUA_IRT;

    /// <summary>80,000,000 per mesghal, in the per-gram unit quotes are stored in.</summary>
    private const decimal AskPerGram = 18_468_073.32m;
    private const decimal BidPerGram = 18_237_000m;

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeOrderApiClient _orderApi = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly InMemoryConversationStore _conversations = new();
    private readonly BotHandler _handler;

    public BotConversationFlowTests()
    {
        _usersApi.User = new UserDto
        {
            Id = CustomerId,
            TelegramId = TelegramId,
            FirstName = "مشتری",
            PhoneNumber = "09121234567",
            Status = UserStatus.Approved,
            Role = UserRole.User
        };

        _handler = new BotHandler(
            NullLogger<BotHandler>.Instance,
            botClient: null!, // only used for the chat-admin lookup, which this flow never reaches
            messenger: _messenger,
            conversations: _conversations,
            orderApi: _orderApi,
            usersApi: _usersApi,
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: new StubWalletApiClient(),
            telegramLogger: new SilentTelegramLogger(),
            versionService: new FakeVersionService());
    }

    // ── driving the conversation ────────────────────────────────────────────────

    private Task SayAsync(string text) => _handler.HandleMessageAsync(new Message
    {
        Text = text,
        Chat = new Chat { Id = ChatId },
        From = new User { Id = TelegramId }
    });

    private Task TapAsync(string callbackData) => _handler.HandleCallbackQueryAsync(new CallbackQuery
    {
        Id = Guid.NewGuid().ToString(),
        Data = callbackData,
        From = new User { Id = TelegramId },
        Message = new Message { Chat = new Chat { Id = ChatId } }
    });

    private void PublishQuote() => _orderApi.ActiveQuote = new QuoteDto
    {
        Id = Guid.NewGuid(),
        Symbol = Gold,
        BuyPrice = BidPerGram,
        SellPrice = AskPerGram,
        PublishedAt = DateTime.UtcNow
    };

    /// <summary>Menu → asset → side → amount. Leaves the confirmation on screen.</summary>
    private async Task WalkToConfirmationAsync(string amount = "2.5", string side = InlineCallBackData.buy_spot)
    {
        await SayAsync(BotBtns.BtnSpotMarket);
        await TapAsync($"asset_{Gold}");
        await TapAsync(side);
        await SayAsync(amount);
    }

    // ── the dealer-model guarantee ──────────────────────────────────────────────

    /// <summary>
    /// The whole point of the dealer model: the customer states a quantity and nothing else.
    /// If a price prompt ever reappeared here, the gram/mesghal ambiguity would come back
    /// with it — and that ambiguity is what made an earlier version show prices off by a
    /// factor of 4.3318.
    /// </summary>
    [Fact]
    public async Task WithAPublishedQuote_TheCustomerIsNeverAskedForAPrice()
    {
        PublishQuote();

        await WalkToConfirmationAsync();

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("قیمت را وارد کنید"));
        Assert.False(_messenger.AnyTextContains(BotMsgs.MsgEnterPriceGold));
    }

    /// <summary>
    /// A buying customer pays the admin's <b>sell</b> price. Swapping the two is the
    /// buyer/seller inversion in another guise, and here it would quote the customer a
    /// price better than the shop ever offered.
    /// </summary>
    [Fact]
    public async Task ABuyingCustomerIsQuotedTheAdminsSellPrice()
    {
        PublishQuote();

        await WalkToConfirmationAsync(side: InlineCallBackData.buy_spot);

        Assert.Equal(AskPerGram, CurrentConversation().Price);
    }

    [Fact]
    public async Task ASellingCustomerIsQuotedTheAdminsBuyPrice()
    {
        PublishQuote();

        await WalkToConfirmationAsync(side: InlineCallBackData.sell_spot);

        Assert.Equal(BidPerGram, CurrentConversation().Price);
    }

    /// <summary>
    /// With no quote published the bot must fall back to asking for a price — the order-book
    /// path. Without this, the previous test would also pass if the confirmation step were
    /// simply broken and no messages were sent at all.
    /// </summary>
    [Fact]
    public async Task WithNoQuotePublished_TheCustomerIsAskedForAPrice()
    {
        _orderApi.ActiveQuote = null;

        await SayAsync(BotBtns.BtnSpotCreateOrder); // limit order, so a price is expected
        await TapAsync($"asset_{Gold}");
        await TapAsync(InlineCallBackData.buy_spot);
        await SayAsync("2.5");

        Assert.Contains(_messenger.Texts, t => t.Contains("مثقال") && t.Contains("قیمت"));
    }

    // ── what the customer is shown, and in what order ───────────────────────────

    [Fact]
    public async Task TheConfirmationShowsTheQuantityAndBothPriceUnits()
    {
        PublishQuote();

        await WalkToConfirmationAsync(amount: "2.5");

        var confirmation = _messenger.Texts.Last();
        Assert.Contains("تأیید سفارش", confirmation);
        Assert.Contains("قیمت هر مثقال", confirmation);
        Assert.Contains("قیمت هر گرم", confirmation);
        Assert.DoesNotContain("{", confirmation);
    }

    /// <summary>
    /// Two messages, in this order: the order was accepted, then the trade executed.
    /// Reversed, the customer is told the outcome of something they have not yet been told
    /// was accepted.
    /// </summary>
    [Fact]
    public async Task OnConfirmation_TheOrderAcknowledgementPrecedesTheTradeReport()
    {
        PublishQuote();
        await WalkToConfirmationAsync();
        var before = _messenger.Sent.Count;

        await TapAsync(InlineCallBackData.confirm_order);

        var after = _messenger.Texts.Skip(before).ToList();

        var acknowledged = after.FindIndex(t => t.Contains("ثبت شد") || t == BotMsgs.MsgOrderSuccess);
        var executed = after.FindIndex(t => t.Contains("معاملهٔ شما انجام شد"));

        Assert.True(acknowledged >= 0, $"no order acknowledgement in: {string.Join(" | ", after)}");
        Assert.True(executed > acknowledged, "the trade report must come after the acknowledgement");
    }

    /// <summary>
    /// In the order-book path the order may rest unfilled for hours, so reporting a trade
    /// would be a lie. The report is tied to a fill, not to the order being accepted.
    /// </summary>
    [Fact]
    public async Task WithoutAQuote_NoTradeReportIsSent_BecauseNothingHasExecuted()
    {
        _orderApi.ActiveQuote = null;
        await SayAsync(BotBtns.BtnSpotCreateOrder);
        await TapAsync($"asset_{Gold}");
        await TapAsync(InlineCallBackData.buy_spot);

        _conversations.TryGet(TelegramId, out var conversation);
        conversation.Amount = 2.5m;
        conversation.Price = AskPerGram;

        await TapAsync(InlineCallBackData.confirm_order);

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("معاملهٔ شما انجام شد"));
    }

    // ── what reaches the Orders service ─────────────────────────────────────────

    /// <summary>
    /// The quantity the customer typed must arrive unchanged. The mesghal conversion applies
    /// to prices, never to quantities — applying it here would place an order 4.3318x the
    /// size the customer asked for.
    /// </summary>
    [Fact]
    public async Task TheQuantityTheCustomerTyped_IsWhatTheOrdersServiceReceives()
    {
        PublishQuote();
        await WalkToConfirmationAsync(amount: "2.5");

        await TapAsync(InlineCallBackData.confirm_order);

        var accepted = Assert.Single(_orderApi.AcceptedQuotes);
        Assert.Equal(2.5m, accepted.Quantity);
        Assert.Equal(Gold, accepted.Symbol);
        Assert.Equal(OrderSide.Buy, accepted.Side);
        Assert.Equal(CustomerId, accepted.UserId);
    }

    /// <summary>
    /// The dealer path accepts a quote; it must not also place an ordinary order. Doing both
    /// would trade twice on one confirmation.
    /// </summary>
    [Fact]
    public async Task TheDealerPathAcceptsTheQuoteAndDoesNotAlsoSubmitAnOrder()
    {
        PublishQuote();
        await WalkToConfirmationAsync();

        await TapAsync(InlineCallBackData.confirm_order);

        Assert.Single(_orderApi.AcceptedQuotes);
        Assert.Empty(_orderApi.SubmittedOrders);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task AnInvalidQuantity_IsRejectedAndNothingIsSentToTheOrdersService(string amount)
    {
        PublishQuote();

        await WalkToConfirmationAsync(amount: amount);

        Assert.Empty(_orderApi.AcceptedQuotes);
        Assert.Contains(_messenger.Texts, t => t.Contains("مقدار معتبر"));
    }

    // ── conversation lifecycle ──────────────────────────────────────────────────

    /// <summary>
    /// The state must be gone once the order is placed. A leftover conversation is what
    /// makes the customer's <i>next</i> order inherit this one's asset, side or amount —
    /// a defect that would look like the bot inventing an order they never asked for.
    /// </summary>
    [Fact]
    public async Task AfterASuccessfulOrder_TheConversationIsCleared()
    {
        PublishQuote();
        await WalkToConfirmationAsync();

        await TapAsync(InlineCallBackData.confirm_order);

        Assert.Equal(0, _conversations.Count);
    }

    /// <summary>Failure must clear it too, or a rejected order strands the customer mid-flow.</summary>
    [Fact]
    public async Task AfterARejectedOrder_TheConversationIsAlsoCleared()
    {
        PublishQuote();
        _orderApi.AcceptResult = (false, "موجودی کافی نیست");
        await WalkToConfirmationAsync();

        await TapAsync(InlineCallBackData.confirm_order);

        Assert.Equal(0, _conversations.Count);
        Assert.Contains(_messenger.Texts, t => t.Contains("موجودی کافی نیست"));
    }

    [Fact]
    public async Task CancellingClearsTheConversation()
    {
        PublishQuote();
        await WalkToConfirmationAsync();

        await TapAsync(InlineCallBackData.cancel_order);

        Assert.Equal(0, _conversations.Count);
    }

    /// <summary>
    /// A button tapped from an old message, after the conversation is gone — a restart, or a
    /// flow that already finished. The customer gets a message rather than the bot throwing.
    /// </summary>
    [Fact]
    public async Task TappingAStaleSideButton_WithNoConversation_DoesNotThrow()
    {
        PublishQuote();

        await TapAsync(InlineCallBackData.buy_spot);

        Assert.NotEmpty(_messenger.Texts);
        Assert.Empty(_orderApi.AcceptedQuotes);
    }

    /// <summary>
    /// Confirming with no conversation must not reach the Orders service. Without the guard
    /// this would place an order with a zero quantity and a zero price.
    /// </summary>
    [Fact]
    public async Task ConfirmingWithNoConversation_PlacesNoOrder()
    {
        PublishQuote();

        await TapAsync(InlineCallBackData.confirm_order);

        Assert.Empty(_orderApi.AcceptedQuotes);
        Assert.Empty(_orderApi.SubmittedOrders);
    }

    private OrderState CurrentConversation()
    {
        Assert.True(_conversations.TryGet(TelegramId, out var conversation), "no conversation in flight");
        return conversation;
    }

    // ── no quote published (market path) ────────────────────────────────────────

    /// <summary>
    /// The dealer/market path never asks for a price, so with no quote published there is
    /// nothing to trade on. Continuing to ask for an amount used to lead the customer into a
    /// guaranteed failure one step later ("خطا در دریافت بهترین قیمت بازار") instead of
    /// telling them up front. Hit live: "دریافت مظنه" on a symbol nobody had published a
    /// price for yet.
    /// </summary>
    [Fact]
    public async Task WithNoQuotePublished_TheMarketPathStopsBeforeAskingForAnAmount()
    {
        _orderApi.ActiveQuote = null;

        await SayAsync(BotBtns.BtnSpotMarket);
        await TapAsync($"asset_{Gold}");

        Assert.Contains(_messenger.Texts, t => t.Contains(BotMsgs.MsgNoQuoteForSymbol));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("لطفاً مقدار را وارد کنید"));
    }

    [Fact]
    public async Task WithNoQuotePublished_TheMarketPathLeavesNoConversationInFlight()
    {
        _orderApi.ActiveQuote = null;

        await SayAsync(BotBtns.BtnSpotMarket);
        await TapAsync($"asset_{Gold}");

        Assert.False(_conversations.TryGet(TelegramId, out _), "a stale conversation should not survive a stop");
    }

    /// <summary>
    /// The limit (order-book) path is unaffected by a missing quote — it never depended on
    /// one, since it asks the customer for a price itself. Only the market path is a dead
    /// end without a quote.
    /// </summary>
    [Fact]
    public async Task WithNoQuotePublished_TheLimitPathStillAsksForAnAmount()
    {
        _orderApi.ActiveQuote = null;

        await SayAsync(BotBtns.BtnSpotCreateOrder);
        await TapAsync($"asset_{Gold}");

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(BotMsgs.MsgNoQuoteForSymbol));
    }

    // ── the back button always escapes ──────────────────────────────────────────

    /// <summary>
    /// A customer stuck typing into "waiting_for_amount" (no quote, a typo, changed their
    /// mind) previously had no way out except knowing the exact literal text to send, and
    /// even that text was parsed as an invalid amount rather than recognised as a command —
    /// there was no button at all on this prompt.
    /// </summary>
    [Fact]
    public async Task TappingBackWhileEnteringAnAmount_ClearsTheConversationInsteadOfBeingParsedAsAnAmount()
    {
        PublishQuote();
        await SayAsync(BotBtns.BtnSpotMarket);
        await TapAsync($"asset_{Gold}");
        await TapAsync(InlineCallBackData.buy_spot);

        await SayAsync(BotBtns.BtnBack);

        Assert.Contains(_messenger.Texts, t => t.Contains("منوی اصلی"));
        Assert.False(_conversations.TryGet(TelegramId, out _), "back must clear the in-flight conversation");
    }

    [Fact]
    public async Task TappingBackWhileEnteringAPrice_ClearsTheConversationInsteadOfBeingParsedAsAPrice()
    {
        _orderApi.ActiveQuote = null;
        await SayAsync(BotBtns.BtnSpotCreateOrder);
        await TapAsync($"asset_{Gold}");
        await TapAsync(InlineCallBackData.buy_spot);
        await SayAsync("2.5"); // a valid amount, which advances the state to waiting_for_price

        await SayAsync(BotBtns.BtnBack);

        Assert.Contains(_messenger.Texts, t => t.Contains("منوی اصلی"));
        Assert.False(_conversations.TryGet(TelegramId, out _), "back must clear the in-flight conversation");
    }

    [Fact]
    public async Task TheAmountPromptOffersABackButton()
    {
        PublishQuote();
        await SayAsync(BotBtns.BtnSpotMarket);
        await TapAsync($"asset_{Gold}");

        await TapAsync(InlineCallBackData.buy_spot);

        var prompt = _messenger.Sent.Last();
        var keyboard = Assert.IsType<ReplyKeyboardMarkup>(prompt.ReplyMarkup);
        Assert.Contains(keyboard.Keyboard.SelectMany(row => row), b => b.Text == BotBtns.BtnBack);
    }

    [Fact]
    public async Task ThePricePromptOffersABackButton()
    {
        _orderApi.ActiveQuote = null;
        await SayAsync(BotBtns.BtnSpotCreateOrder);
        await TapAsync($"asset_{Gold}");
        await TapAsync(InlineCallBackData.buy_spot);

        await SayAsync("2.5");

        var prompt = _messenger.Sent.Last();
        var keyboard = Assert.IsType<ReplyKeyboardMarkup>(prompt.ReplyMarkup);
        Assert.Contains(keyboard.Keyboard.SelectMany(row => row), b => b.Text == BotBtns.BtnBack);
    }
}
