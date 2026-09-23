using Microsoft.Extensions.Logging;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.AllServices.Tests.Fakes;
using Telegram.Bot.Types;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What the bot's order-book path tells a customer when it cannot fund their order (issue #290).
///
/// <para>
/// One guard joined two different answers: <c>if (!success || !hasEnough)</c>, reported with the
/// wallet client's own <c>Message</c> pasted into an insufficient-funds sentence. So a customer
/// whose balance really was too low read a log status line — «اعتبار و موجودی کاربر بررسی شد» —
/// as the reason, and a customer who hit a wallet outage was told their funds were short and to
/// call the shop for more credit. The same shape was #284 on the service's own order endpoint.
/// </para>
///
/// <para>
/// Not reachable today: every symbol trades in dealer mode and the quote branch returns before this
/// step. It becomes reachable the moment a symbol runs on the order book — which
/// <c>docs/product/DIRECTION.md</c> records as something the owner may bring back.
/// </para>
///
/// <para>
/// <c>StubWalletApiClient.ValidateCreditAndBalanceAsync</c> throws <c>NotSupportedException</c>, so
/// nothing exercised this path at all before these tests. That is the coverage gap the defect lived
/// in, and the reason the stub is subclassed here rather than changed: it should keep throwing for
/// every test that has no business reaching the wallet.
/// </para>
/// </summary>
public class OrderBookBalanceRefusalTests
{
    private const long ChatId = 556_000;
    private const long TelegramId = 556_000;
    private const string Gold = CurrenciesConstant.MAUA_IRT;

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeOrderApiClient _orderApi = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly InMemoryConversationStore _conversations = new();
    private readonly CapturingLogger<BotHandler> _logger = new();

    /// <summary>
    /// The wallet's two distinguishable answers, and nothing else.
    ///
    /// <para>
    /// <b>This stub is only trustworthy because the real client is pinned separately.</b> The first
    /// version of these tests invented the outage tuple — <c>Success = false</c> — and the real
    /// <c>WalletApiClient</c> never produced it: every failure was swallowed into "balance zero,
    /// check succeeded". Three tests here were green against behaviour that did not exist.
    /// <c>WalletUnreachableIsNotZeroBalanceTests</c> drives the real client over a stubbed
    /// transport and is what makes the shape below true; if that file is ever deleted, these tests
    /// go back to proving nothing.
    /// </para>
    /// </summary>
    private sealed class WalletStub : StubWalletApiClient
    {
        public bool CheckSucceeds { get; set; } = true;
        public bool HasEnough { get; set; } = true;

        /// <summary>
        /// The status line the client returns on a successful check. This is the text that used to
        /// be shown to the customer as the reason their order was refused.
        /// </summary>
        public const string SuccessStatusLine = "اعتبار و موجودی کاربر بررسی شد";

        /// <summary>And what it returns when it could not reach the wallet at all.</summary>
        public const string OutageMessage = "خطا در ارتباط با سرویس کیف پول";

        public override Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
            ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price) =>
            Task.FromResult(CheckSucceeds
                ? (true, SuccessStatusLine, HasEnough, HasEnough)
                : (false, OutageMessage, false, false));
    }

    private readonly WalletStub _wallet = new();
    private readonly BotHandler _handler;

    public OrderBookBalanceRefusalTests()
    {
        _usersApi.User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = TelegramId,
            FirstName = "مشتری",
            PhoneNumber = "09121234567",
            Status = UserStatus.Approved,
            Role = UserRole.User      // not an operator: operators skip the check entirely
        };

        _handler = new BotHandler(
            _logger,
            botClient: null!,
            messenger: _messenger,
            conversations: _conversations,
            orderApi: _orderApi,
            usersApi: _usersApi,
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: _wallet,
            versionService: new FakeVersionService());
    }

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

    /// <summary>
    /// Menu → asset → side → amount → price. No published quote, so the amount step takes the
    /// order-book branch and asks for a price; entering one reaches the guard under test.
    /// </summary>
    private async Task EnterAnOrderBookOrderAsync()
    {
        _orderApi.ActiveQuote = null;
        await SayAsync(BotBtns.BtnSpotCreateOrder);
        await TapAsync($"asset_{Gold}");
        await TapAsync(InlineCallBackData.buy_spot);
        await SayAsync("2.5");
        await SayAsync("80000000");
    }

    // ── the check ran, and the customer cannot afford it ────────────────────────

    [Fact]
    public async Task WhenTheCustomerCannotAfford_TheWalletsStatusLineIsNotShownToThem()
    {
        _wallet.HasEnough = false;

        await EnterAnOrderBookOrderAsync();

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(WalletStub.SuccessStatusLine));
    }

    [Fact]
    public async Task WhenTheCustomerCannotAfford_TheyAreToldTheirFundsAreShort()
    {
        _wallet.HasEnough = false;

        await EnterAnOrderBookOrderAsync();

        Assert.Contains(_messenger.Texts, t => t.Contains(RefusalMessages.InsufficientFunds));
    }

    // ── the check could not run at all ──────────────────────────────────────────

    /// <summary>
    /// The expensive half. Telling a customer their balance is short when the wallet is simply
    /// unreachable sends them to top up an account that was never the problem — and the old message
    /// closed with «برای افزایش موجودی یا اعتبار، با طلافروشی خود تماس بگیرید».
    /// </summary>
    [Fact]
    public async Task WhenTheWalletCannotBeReached_TheCustomerIsNotToldTheirFundsAreShort()
    {
        _wallet.CheckSucceeds = false;

        await EnterAnOrderBookOrderAsync();

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(RefusalMessages.InsufficientFunds));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("با طلافروشی خود تماس بگیرید"));
    }

    [Fact]
    public async Task WhenTheWalletCannotBeReached_TheCustomerIsAskedToTryAgain()
    {
        _wallet.CheckSucceeds = false;

        await EnterAnOrderBookOrderAsync();

        Assert.Contains(_messenger.Texts, t => t.Contains(BotMsgs.MsgBalanceCheckFailed));
    }

    /// <summary>
    /// And the outage is recorded. A refusal a customer cannot act on is one an operator has to
    /// find later, and the wallet client's own message is the only clue to why.
    /// </summary>
    [Fact]
    public async Task WhenTheWalletCannotBeReached_TheOutageIsLoggedAsAWarning()
    {
        _wallet.CheckSucceeds = false;

        await EnterAnOrderBookOrderAsync();

        Assert.Contains(_logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains(WalletStub.OutageMessage));
    }

    // ── and nothing from the wallet client reaches the customer, on either branch ──

    /// <summary>
    /// The property that covers both defects at once, and the one a weaker test misses.
    ///
    /// <para>
    /// A first version of this asserted only that the two refusals differ. It passed before the fix
    /// as well as after — because they did differ, by exactly the wallet client's own message pasted
    /// into each. Differing for the wrong reason is the defect, so the assertion has to be that the
    /// client's text appears in neither.
    /// </para>
    ///
    /// <para>
    /// The wallet client's <c>Message</c> is a diagnostic. On the success branch it is a log status
    /// line, on the failure branch an internal English-or-Persian error; neither is written for a
    /// customer. It belongs in the log, which is what the warning above checks.
    /// </para>
    /// </summary>
    [Fact]
    public async Task NeitherRefusalRepeatsTheWalletClientsOwnMessage()
    {
        _wallet.HasEnough = false;
        await EnterAnOrderBookOrderAsync();

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(WalletStub.SuccessStatusLine));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(WalletStub.OutageMessage));

        _conversations.Clear(TelegramId);
        _messenger.Sent.Clear();

        _wallet.CheckSucceeds = false;
        await EnterAnOrderBookOrderAsync();

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(WalletStub.SuccessStatusLine));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(WalletStub.OutageMessage));
    }
}
