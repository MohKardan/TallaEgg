using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using Wallet.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace Wallet.Tests;

/// <summary>
/// "💹 اعلام مظنه" is on the operator's main menu. Quotes are published with the
/// "buyPrice-sellPrice" command or the auto-quote publisher (#90) — never through this button
/// — so for an operator it now shows the latest published quote instead of walking them through
/// the customer order flow, which answered a question nobody asked.
/// </summary>
public class AnnounceQuoteButtonTests
{
    private const long TelegramId = 5001;
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly FakeOrderApiClient _orderApi = new();
    private readonly InMemoryConversationStore _conversations = new();

    private BotHandler Build(UserRole callerRole)
    {
        _usersApi.User = new UserDto
        {
            Id = UserId,
            TelegramId = TelegramId,
            FirstName = "کاربر",
            PhoneNumber = "09000000000",
            Status = UserStatus.Approved,
            Role = callerRole
        };

        return new BotHandler(
            NullLogger<BotHandler>.Instance,
            botClient: null!,
            messenger: _messenger,
            conversations: _conversations,
            orderApi: _orderApi,
            usersApi: _usersApi,
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: new StubWalletApiClient(),
            telegramLogger: new SilentTelegramLogger(),
            versionService: new FakeVersionService());
    }

    private Task PressAsync(BotHandler handler) =>
        handler.HandleMessageAsync(new Message
        {
            Text = BotBtns.BtnSpotSubmitPrice,
            Chat = new Chat { Id = TelegramId },
            From = new User { Id = TelegramId }
        });

    [Fact]
    public async Task AnOperatorSeesTheLatestQuoteRatherThanTheOrderFlow()
    {
        _orderApi.QuoteHistory.Add(new QuoteDto
        {
            Symbol = "MAUA/IRT",
            BuyPrice = 18_000_000m,
            SellPrice = 18_100_000m,
            IsActive = true,
            PublishedAt = DateTime.UtcNow
        });

        var handler = Build(UserRole.Admin);

        await PressAsync(handler);

        Assert.Contains(_messenger.Texts, t => t.Contains("مظنهٔ فعال"));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains(BotMsgs.MsgSelectAsset));
    }

    [Fact]
    public async Task AnOperatorWithNoPublishedQuoteIsToldSo()
    {
        var handler = Build(UserRole.Admin);

        await PressAsync(handler);

        Assert.Contains(_messenger.Texts, t => t.Contains("هنوز مظنه‌ای منتشر نشده است"));
    }

    /// <summary>
    /// This button is only ever offered to operators, but its text is replayable like any
    /// other message — a non-operator sending it must still land in the ordinary flow, not
    /// see operator-only quote data.
    /// </summary>
    [Fact]
    public async Task ANonOperatorStillGetsTheOrdinaryOrderFlow()
    {
        _orderApi.QuoteHistory.Add(new QuoteDto { Symbol = "MAUA/IRT", IsActive = true });

        var handler = Build(UserRole.RegularUser);

        await PressAsync(handler);

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("مظنهٔ فعال"));
        Assert.Contains(_messenger.Texts, t => t.Contains(BotMsgs.MsgSelectAsset));
    }
}
