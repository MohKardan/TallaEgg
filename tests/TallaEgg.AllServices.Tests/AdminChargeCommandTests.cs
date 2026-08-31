using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Core.Utilties;
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

        await SayAsync(handler, "د 09158527483 100 آبشده");

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

    /// <summary>
    /// The figure a deduplicated repeat shows has to be what the customer holds now, not what the
    /// original operation left behind. Found against the running bot: a deduction, then a second
    /// larger deduction, then the first one re-sent — the reply quoted the balance from before the
    /// second deduction, labelled as the current one.
    /// </summary>
    [Fact]
    public async Task ADeduplicatedChargeQuotesTheBalanceAsItStandsNow()
    {
        var handler = Build();
        _walletApi.ReportAlreadyApplied = true;
        _walletApi.CurrentBalance = 700m;          // the wallet has moved on since

        await SayAsync(handler, "ش 09158527483 100 سکه");

        var reply = Assert.Single(_messenger.Texts, t => t.Contains("پیش‌تر ثبت شده بود"));
        Assert.Contains(PersianFormat.Amount(700m, "CREDIT_SEKE_BAHAR"), reply);
        Assert.DoesNotContain("کنونی کاربر: " + PersianFormat.Amount(100m, "CREDIT_SEKE_BAHAR"), reply);
    }

    /// <summary>The deduction command carries the same correction.</summary>
    [Fact]
    public async Task ADeduplicatedDeductionQuotesTheBalanceAsItStandsNow()
    {
        var handler = Build();
        _walletApi.ReportAlreadyApplied = true;
        _walletApi.CurrentBalance = 4_200m;

        await SayAsync(handler, "د 09158527483 500 آبشده");

        var reply = Assert.Single(_messenger.Texts, t => t.Contains("پیش‌تر ثبت شده بود"));
        Assert.Contains(PersianFormat.Amount(4_200m, "CREDIT_MAUA"), reply);
    }

    /// <summary>
    /// An ordinary charge quotes the balance it just produced, not the live one. The two are the
    /// same number in practice, so the stub is given a deliberately different CurrentBalance —
    /// otherwise this passes whether or not the handler still distinguishes the two cases.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryChargeStillQuotesWhatItJustProduced()
    {
        var handler = Build();
        _walletApi.CurrentBalance = 999m;          // must not appear: this charge was applied

        await SayAsync(handler, "ش 09158527483 100 سکه");

        var reply = Assert.Single(_messenger.Texts, t => t.Contains("افزایش اعتبار انجام شد"));
        Assert.Contains(PersianFormat.Amount(100m, "CREDIT_SEKE_BAHAR"), reply);
        Assert.DoesNotContain(PersianFormat.Amount(999m, "CREDIT_SEKE_BAHAR"), reply);
    }

    /// <summary>
    /// A wallet too old to send CurrentBalance must not turn into a reported balance of zero. The
    /// services are installed and restarted individually, so a bot running ahead of the wallet is
    /// an ordinary deployment state, and the reply falls back to the figure the older wallet does
    /// send rather than to nothing.
    /// </summary>
    [Fact]
    public async Task AWalletThatSendsNoCurrentBalanceFallsBackInsteadOfReportingZero()
    {
        var handler = Build();
        _walletApi.ReportAlreadyApplied = true;
        _walletApi.OmitCurrentBalance = true;

        await SayAsync(handler, "ش 09158527483 100 سکه");

        var reply = Assert.Single(_messenger.Texts, t => t.Contains("پیش‌تر ثبت شده بود"));
        // Anchored to its label: a bare PersianFormat.Amount(0) is "۰", which also sits inside "۱۰۰".
        Assert.Contains("اعتبار کنونی کاربر: " + PersianFormat.Amount(100m, "CREDIT_SEKE_BAHAR"), reply);
    }
    // -----------------------------------------------------------------------------------
    // ش and د as mirrors (evidence recorded on #36).
    //
    // They used to write to different wallets from the same Persian word: ش credited
    // CREDIT_<X> while د debited plain <X>, with identical help text and identical examples.
    // An admin undoing a mistaken top-up with the obvious symmetric command left the credit
    // untouched and took the customer's real position instead. Nothing in this suite covered
    // which asset د targeted, so the whole asymmetry was invisible to it.
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task DeductingTargetsTheCreditLedgerNotTheSpotWallet()
    {
        var handler = Build();

        await SayAsync(handler, "د 09158527483 100 سکه");

        Assert.Equal("CREDIT_SEKE_BAHAR", Assert.Single(_walletApi.Withdrawals).Asset);
    }

    /// <summary>The property that was broken, stated directly: the same words reach the same wallet.</summary>
    [Theory]
    [InlineData("سکه")]
    [InlineData("آبشده")]
    [InlineData("بیت‌کوین")]
    [InlineData("")]
    public async Task ChargingAndDeductingTheSameWordsReachTheSameWallet(string asset)
    {
        var handler = Build();
        var suffix = asset.Length == 0 ? "" : " " + asset;

        await SayAsync(handler, "ش 09158527483 100" + suffix);
        await SayAsync(handler, "د 09158527483 100" + suffix);

        Assert.Equal(Assert.Single(_walletApi.Deposits).Asset, Assert.Single(_walletApi.Withdrawals).Asset);
    }

    /// <summary>
    /// With no asset named, both default to gold. د used to default to Toman, so even the
    /// shorthand forms disagreed about what they meant.
    /// </summary>
    [Fact]
    public async Task DeductingWithNoCurrencyGivenDefaultsToGoldCredit()
    {
        var handler = Build();

        await SayAsync(handler, "د 09158527483 100");

        Assert.Equal("CREDIT_MAUA", Assert.Single(_walletApi.Withdrawals).Asset);
    }

    /// <summary>
    /// Naming the credit ledger is refused on both commands, rather than being prefixed a second
    /// time into CREDIT_CREDIT_MAUA — an asset that does not exist and would fail at the wallet
    /// with a confusing "wallet not found". Both commands already mean the credit ledger.
    /// </summary>
    [Theory]
    [InlineData("ش")]
    [InlineData("د")]
    public async Task NamingTheCreditLedgerIsRefusedRatherThanDoublePrefixed(string command)
    {
        var handler = Build();

        await SayAsync(handler, command + " 09158527483 100 اعتبار آبشده");

        Assert.Empty(_walletApi.Deposits);
        Assert.Empty(_walletApi.Withdrawals);

        // Its own sentence, not "unrecognised": the admin typed something meaningful, and being
        // told to check their spelling would send them looking for a mistake that is not there.
        Assert.Contains(_messenger.Texts, t => t.Contains("لازم نیست"));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("شناسایی نشد"));
    }

    /// <summary>
    /// Toman has no credit ledger. Credit ledgers are minted per tradable base asset, and Toman is
    /// a quote currency — CREDIT_IRT is not a currency, and the wallet rejects a deposit into it.
    ///
    /// <para>
    /// This was already true of the top-up command before these commands were made mirrors: it has
    /// always built CREDIT_IRT for "تومان" and always failed, opaquely, at the wallet — while the
    /// bot's own help offered exactly that as its example. Both commands now say so directly.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("ش")]
    [InlineData("د")]
    public async Task TomanIsRefusedBecauseItHasNoCreditLedger(string command)
    {
        var handler = Build();

        await SayAsync(handler, command + " 09158527483 500 تومان");

        Assert.Empty(_walletApi.Deposits);
        Assert.Empty(_walletApi.Withdrawals);

        // Not "unrecognised" either: "تومان" is a real asset, it simply cannot carry credit.
        Assert.Contains(_messenger.Texts, t => t.Contains("دفتر اعتبار ندارد"));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("شناسایی نشد"));
    }

    /// <summary>An unrecognised word still gets the spelling message, so the three refusals stay distinct.</summary>
    [Fact]
    public async Task AnUnrecognisedWordStillGetsTheSpellingMessage()
    {
        var handler = Build();

        await SayAsync(handler, "ش 09158527483 100 نقره");

        Assert.Empty(_walletApi.Deposits);
        Assert.Contains(_messenger.Texts, t => t.Contains("شناسایی نشد"));
    }

    /// <summary>
    /// The customer is told their credit fell, not their balance. The wording followed the old
    /// behaviour and would otherwise now describe a wallet the command no longer touches.
    /// </summary>
    [Fact]
    public async Task TheCustomerIsToldTheirCreditFellNotTheirBalance()
    {
        var handler = Build();

        await SayAsync(handler, "د 09158527483 100 سکه");

        Assert.Contains(_messenger.Texts, t => t.Contains("اعتبار حساب شما کاهش یافت"));
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("از موجودی حساب شما کسر شد"));
    }

    /// <summary>And the admin's own confirmation says credit too.</summary>
    [Fact]
    public async Task TheAdminConfirmationSaysCredit()
    {
        var handler = Build();

        await SayAsync(handler, "د 09158527483 100 سکه");

        Assert.Contains(_messenger.Texts, t => t.Contains("کسر از اعتبار انجام شد"));
    }

    /// <summary>
    /// The deduplication key follows the ledger the command actually touches, so a top-up and a
    /// deduction of the same amount still cannot be mistaken for one another.
    /// </summary>
    [Fact]
    public async Task TheDeductionKeyNamesTheCreditLedger()
    {
        var handler = Build();

        await SayAsync(handler, "د 09158527483 100 سکه");

        var withdrawal = Assert.Single(_walletApi.Withdrawals);
        Assert.StartsWith("admin-withdrawal:", withdrawal.ReferenceId);
        Assert.Contains("CREDIT_SEKE_BAHAR", withdrawal.ReferenceId);
    }
    private sealed class RecordingWalletApiClient : StubWalletApiClient
    {
        public List<WalletRequest> Deposits { get; } = [];
        public List<WalletRequest> Withdrawals { get; } = [];

        /// <summary>Makes the wallet answer the way it does for a reference it has already applied.</summary>
        public bool ReportAlreadyApplied { get; set; }

        /// <summary>
        /// What the wallet holds now. Set it apart from the amount to reproduce the case that
        /// matters: a repeat whose original BalanceAfter is no longer what the customer holds.
        /// </summary>
        public decimal? CurrentBalance { get; set; }

        /// <summary>Answers the way a wallet that predates the field does: without it at all.</summary>
        public bool OmitCurrentBalance { get; set; }

        public override Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request)
        {
            Deposits.Add(request);
            return Task.FromResult(TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Ok(new WalletBallanceDTO
            {
                Asset = request.Asset,
                BalanceBefore = 0,
                BalanceAfter = request.Amount,
                WasAlreadyApplied = ReportAlreadyApplied,
                CurrentBalance = OmitCurrentBalance ? null : CurrentBalance ?? request.Amount
            }));
        }

        public override Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> WithdrawalAsync(WalletRequest request)
        {
            Withdrawals.Add(request);
            return Task.FromResult(TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Ok(new WalletBallanceDTO
            {
                Asset = request.Asset,
                BalanceBefore = request.Amount,
                BalanceAfter = 0,
                WasAlreadyApplied = ReportAlreadyApplied,
                CurrentBalance = OmitCurrentBalance ? null : CurrentBalance ?? 0m
            }));
        }
    }
}
