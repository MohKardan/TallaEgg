using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
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
/// <c>اسپرد [درصد]</c> and <c>اتومات روشن</c>/<c>اتومات خاموش</c> — the two admin commands that
/// control automatic quote publishing from inside the bot (issue #90). These only need to prove
/// the parse and the delegation to <see cref="TallaEgg.TelegramBot.Infrastructure.Clients.IOrderApiClient"/>;
/// the actual settings logic is covered on the Orders side by <c>AutoQuoteSettingsTests</c> and
/// <c>AutoQuotePublisherServiceTests</c>.
/// </summary>
public class AutoQuoteCommandTests
{
    private const long AdminTelegramId = 5001;
    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly FakeOrderApiClient _orderApi = new();

    private BotHandler Build(UserRole callerRole = UserRole.Admin)
    {
        _usersApi.User = new UserDto
        {
            Id = AdminId,
            TelegramId = AdminTelegramId,
            FirstName = "مدیر",
            PhoneNumber = "09000000000",
            Status = UserStatus.Approved,
            Role = callerRole
        };

        return new BotHandler(
            NullLogger<BotHandler>.Instance,
            botClient: null!,
            messenger: _messenger,
            conversations: new InMemoryConversationStore(),
            orderApi: _orderApi,
            usersApi: _usersApi,
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: new StubWalletApiClient(),
            telegramLogger: new SilentTelegramLogger(),
            versionService: new FakeVersionService());
    }

    private Task SayAsync(BotHandler handler, string text) =>
        handler.HandleMessageAsync(new Message
        {
            Text = text,
            Chat = new Chat { Id = AdminTelegramId },
            From = new User { Id = AdminTelegramId }
        });

    // ── اسپرد ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnAdminCanSetTheSpread()
    {
        var handler = Build();

        await SayAsync(handler, "اسپرد 0.5");

        var update = Assert.Single(_orderApi.SpreadUpdates);
        Assert.Equal(CurrenciesConstant.MAUA_IRT, update.Symbol);
        Assert.Equal(0.5m, update.SpreadPercent);
        Assert.Equal(AdminId, update.UpdatedByUserId);
    }

    [Fact]
    public async Task AMalformedSpreadCommandIsAnsweredWithTheFormat()
    {
        var handler = Build();

        await SayAsync(handler, "اسپرد بالا");

        Assert.Empty(_orderApi.SpreadUpdates);
        Assert.Contains(_messenger.Texts, t => t.Contains("قالب"));
    }

    /// <summary>
    /// A trailing symbol keyword targets a different symbol's auto-quote settings — added
    /// alongside the coin and Bitcoin symbols. No keyword still means MAUA/IRT, covered above.
    /// </summary>
    [Fact]
    public async Task AnAdminCanSetTheSpreadForACoinOrBitcoin()
    {
        var handler = Build();

        await SayAsync(handler, "اسپرد 0.5 سکه");

        var update = Assert.Single(_orderApi.SpreadUpdates);
        Assert.Equal(CurrenciesConstant.SEKE_BAHAR_IRT, update.Symbol);
        Assert.Equal(0.5m, update.SpreadPercent);
    }

    [Fact]
    public async Task AnUnrecognisedSymbolKeywordIsRejectedWithoutUpdatingAnything()
    {
        var handler = Build();

        await SayAsync(handler, "اسپرد 0.5 نقره");

        Assert.Empty(_orderApi.SpreadUpdates);
        Assert.Contains(_messenger.Texts, t => t.Contains("شناخته‌شده نیست"));
    }

    [Fact]
    public async Task AFailureSettingTheSpreadIsReportedWithItsReason()
    {
        var handler = Build();
        _orderApi.SpreadUpdateResult = (false, "نماد یافت نشد.");

        await SayAsync(handler, "اسپرد 0.5");

        Assert.Contains(_messenger.Texts, t => t.Contains("نماد یافت نشد."));
    }

    /// <summary>
    /// The dispatch guard requires the trailing space, the same way <c>"ن "</c> does — so an
    /// unrelated word that happens to start the same way falls through to the normal menu
    /// instead of being answered with a format error.
    /// </summary>
    [Fact]
    public async Task AWordStartingWithSpreadButNotFollowedBySpaceIsNotTreatedAsThisCommand()
    {
        var handler = Build();

        await SayAsync(handler, "اسپردهای بازار زیاده");

        Assert.Empty(_orderApi.SpreadUpdates);
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("قالب دستور درست نیست"));
    }

    // ── اتومات ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnAdminCanTurnAutoQuoteOn()
    {
        var handler = Build();

        await SayAsync(handler, "اتومات روشن");

        var toggle = Assert.Single(_orderApi.EnabledToggles);
        Assert.Equal(CurrenciesConstant.MAUA_IRT, toggle.Symbol);
        Assert.True(toggle.IsEnabled);
        Assert.Equal(AdminId, toggle.UpdatedByUserId);
    }

    [Fact]
    public async Task AnAdminCanTurnAutoQuoteOff()
    {
        var handler = Build();

        await SayAsync(handler, "اتومات خاموش");

        Assert.False(Assert.Single(_orderApi.EnabledToggles).IsEnabled);
    }

    [Fact]
    public async Task AnAdminCanTurnAutoQuoteOnForBitcoin()
    {
        var handler = Build();

        await SayAsync(handler, "اتومات روشن بیت");

        var toggle = Assert.Single(_orderApi.EnabledToggles);
        Assert.Equal(CurrenciesConstant.BTC_IRT, toggle.Symbol);
        Assert.True(toggle.IsEnabled);
    }

    [Fact]
    public async Task AMalformedToggleCommandIsAnsweredWithTheFormat()
    {
        var handler = Build();

        await SayAsync(handler, "اتومات شاید");

        Assert.Empty(_orderApi.EnabledToggles);
        Assert.Contains(_messenger.Texts, t => t.Contains("قالب"));
    }

    // ── who may run these ───────────────────────────────────────────────────────

    [Fact]
    public async Task AnOrdinaryUserCannotChangeTheSpread()
    {
        var handler = Build(UserRole.RegularUser);

        await SayAsync(handler, "اسپرد 0.5");

        Assert.Empty(_orderApi.SpreadUpdates);
    }

    [Fact]
    public async Task AnOrdinaryUserCannotToggleAutoQuote()
    {
        var handler = Build(UserRole.RegularUser);

        await SayAsync(handler, "اتومات روشن");

        Assert.Empty(_orderApi.EnabledToggles);
    }
}
