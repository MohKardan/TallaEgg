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
using TallaEgg.AllServices.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

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

    // -----------------------------------------------------------------------------------
    // Deduplication key (issue #157).
    //
    // This command sent Asset, Amount and UserId and nothing else, so every deposit row in
    // production held a null ReferenceId — confirmed against the local database, where all 42
    // deposit and withdrawal rows were null. With no key there was nothing for a unique index
    // or an endpoint check to compare, and an admin who re-sent after a lost reply credited
    // the customer twice.
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task ChargingSendsADeduplicationKey()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 سکه");

        var deposit = Assert.Single(_walletApi.Deposits);
        Assert.False(string.IsNullOrWhiteSpace(deposit.ReferenceId));
        Assert.StartsWith("admin-deposit:", deposit.ReferenceId);
    }

    /// <summary>
    /// The lost-response case: the admin sees no confirmation and types the command again. Two
    /// different Telegram messages, so a key derived from the message id would differ and
    /// deduplicate nothing — the key has to come from the content, and does.
    /// </summary>
    [Fact]
    public async Task TheSameChargeSentTwiceCarriesTheSameKey()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 سکه");
        await SayAsync(handler, "ش 09158527483 100 سکه");

        Assert.Equal(2, _walletApi.Deposits.Count);
        Assert.Equal(_walletApi.Deposits[0].ReferenceId, _walletApi.Deposits[1].ReferenceId);
    }

    [Fact]
    public async Task ChargingDifferentAmountsCarriesDifferentKeys()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 سکه");
        await SayAsync(handler, "ش 09158527483 200 سکه");

        Assert.NotEqual(_walletApi.Deposits[0].ReferenceId, _walletApi.Deposits[1].ReferenceId);
    }

    /// <summary>The key is over the credit ledger actually charged, not the currency as typed.</summary>
    [Fact]
    public async Task ChargingDifferentAssetsCarriesDifferentKeys()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 سکه");
        await SayAsync(handler, "ش 09158527483 100 بیت‌کوین");

        Assert.NotEqual(_walletApi.Deposits[0].ReferenceId, _walletApi.Deposits[1].ReferenceId);
    }

    [Fact]
    public async Task DeductingSendsItsOwnDeduplicationKey()
    {
        var handler = Build();

        await SayAsync(handler, "د 09158527483 100 تومان");

        var withdrawal = Assert.Single(_walletApi.Withdrawals);
        Assert.StartsWith("admin-withdrawal:", withdrawal.ReferenceId);
    }

    /// <summary>
    /// A deduplicated repeat still reports success, because the charge did happen — on the earlier
    /// send. Telling the customer their credit rose again would be a lie about their own money, so
    /// only the admin is told, and told that it was a repeat rather than a fresh charge.
    /// </summary>
    [Fact]
    public async Task ADeduplicatedChargeTellsTheAdminButNotTheCustomer()
    {
        var handler = Build();
        _walletApi.ReportAlreadyApplied = true;

        await SayAsync(handler, "ش 09158527483 100 سکه");

        Assert.Contains(_messenger.Texts, t => t.Contains("پیش‌تر ثبت شده بود"));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("اعتبار حساب شما افزایش یافت"));
    }

    /// <summary>And an ordinary charge still notifies both, which is the behaviour that must not regress.</summary>
    [Fact]
    public async Task AnOrdinaryChargeStillTellsBoth()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 سکه");

        Assert.Contains(_messenger.Texts, t => t.Contains("افزایش اعتبار انجام شد"));
        Assert.Contains(_messenger.Texts, t => t.Contains("اعتبار حساب شما افزایش یافت"));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("پیش‌تر ثبت شده بود"));
    }

    private sealed class RecordingWalletApiClient : StubWalletApiClient
    {
        public List<WalletRequest> Deposits { get; } = [];
        public List<WalletRequest> Withdrawals { get; } = [];

        /// <summary>Makes the wallet answer the way it does for a reference it has already applied.</summary>
        public bool ReportAlreadyApplied { get; set; }

        public override Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request)
        {
            Deposits.Add(request);
            return Task.FromResult(TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Ok(new WalletBallanceDTO
            {
                Asset = request.Asset,
                BalanceBefore = 0,
                BalanceAfter = request.Amount,
                WasAlreadyApplied = ReportAlreadyApplied
            }));
        }

        public override Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> WithdrawalAsync(WalletRequest request)
        {
            Withdrawals.Add(request);
            return Task.FromResult(TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Ok(new WalletBallanceDTO
            {
                Asset = request.Asset,
                BalanceBefore = request.Amount,
                BalanceAfter = 0
            }));
        }
    }
}
