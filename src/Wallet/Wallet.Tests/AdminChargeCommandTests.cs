using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using Wallet.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace Wallet.Tests;

/// <summary>
/// <c>ش [تلفن] [مقدار] [نوع]</c> — the admin charge command. Hit live after #111/#112 added the
/// coin and Bitcoin as tradable symbols: "ش 09158527483 100 سکه" answered "نوع شناسایی نشد", and
/// the full Persian name ("سکه تمام بهار آزادی") didn't even parse, because the currency slot
/// only ever accepted one whitespace-free token and matched it against an exact code or the full
/// Persian name — never the short alias the quote commands already accept.
/// </summary>
public class AdminChargeCommandTests
{
    private const long AdminTelegramId = 5001;
    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly RecordingWalletApiClient _walletApi = new();

    private BotHandler Build()
    {
        _usersApi.User = new UserDto
        {
            Id = AdminId,
            TelegramId = AdminTelegramId,
            FirstName = "مدیر",
            PhoneNumber = "09158527483",
            Status = UserStatus.Approved,
            Role = UserRole.Admin
        };

        return new BotHandler(
            NullLogger<BotHandler>.Instance,
            botClient: null!,
            messenger: _messenger,
            conversations: new InMemoryConversationStore(),
            orderApi: new FakeOrderApiClient(),
            usersApi: _usersApi,
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: _walletApi,
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

    [Fact]
    public async Task ChargingWithTheShortCoinAlias_CreditsTheCoinsCreditLedger()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 سکه");

        var deposit = Assert.Single(_walletApi.Deposits);
        Assert.Equal("CREDIT_SEKE_BAHAR", deposit.Asset);
        Assert.Equal(100m, deposit.Amount);
    }

    /// <summary>The multi-word regex fix — the full Persian name must parse, not just one token.</summary>
    [Fact]
    public async Task ChargingWithTheFullMultiWordPersianName_Parses()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 سکه تمام بهار آزادی");

        var deposit = Assert.Single(_walletApi.Deposits);
        Assert.Equal("CREDIT_SEKE_BAHAR", deposit.Asset);
    }

    [Fact]
    public async Task ChargingBitcoin_CreditsBitcoinsCreditLedgerNotGold()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 بیت‌کوین");

        Assert.Equal("CREDIT_BTC", Assert.Single(_walletApi.Deposits).Asset);
    }

    [Fact]
    public async Task WithNoCurrencyGiven_DefaultsToGold()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100");

        Assert.Equal("CREDIT_MAUA", Assert.Single(_walletApi.Deposits).Asset);
    }

    [Fact]
    public async Task AnUnresolvableCurrencyIsRejectedWithoutDepositing()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 نقره");

        Assert.Empty(_walletApi.Deposits);
        Assert.Contains(_messenger.Texts, t => t.Contains("شناسایی نشد"));
    }

    private sealed class RecordingWalletApiClient : StubWalletApiClient
    {
        public List<WalletRequest> Deposits { get; } = [];

        public override Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request)
        {
            Deposits.Add(request);
            return Task.FromResult(TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Ok(new WalletBallanceDTO
            {
                Asset = request.Asset,
                BalanceBefore = 0,
                BalanceAfter = request.Amount
            }));
        }
    }
}
