using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram;
using Telegram.Bot.Types;
using TallaEgg.AllServices.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What the bot says while somebody is joining — found by walking through it as a new user on
/// a freshly wiped database.
///
/// <para>
/// None of these are logic faults; all three are things the person on the other end reads, and
/// all three were wrong in a way that only shows when the database is empty enough that you
/// have to register from scratch.
/// </para>
/// </summary>
public class RegistrationMessagingTests
{
    private const long OwnerTelegramId = 6389449308;
    private const long StrangerTelegramId = 262176247;

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();

    private BotHandler Build(IEnumerable<long>? owners = null) => new(
        NullLogger<BotHandler>.Instance,
        botClient: null!,
        messenger: _messenger,
        conversations: new InMemoryConversationStore(),
        orderApi: new FakeOrderApiClient(),
        usersApi: _usersApi,
        affiliateApi: new FakeAffiliateApiClient(),
        walletApi: new StubWalletApiClient(),
        telegramLogger: new SilentTelegramLogger(),
        versionService: new FakeVersionService(),
        ownerTelegramIds: owners);

    private Task SayAsync(BotHandler handler, string text, long from) =>
        handler.HandleMessageAsync(new Message
        {
            Text = text,
            Chat = new Chat { Id = from },
            From = new User { Id = from }
        });

    private Task ShareContactAsync(BotHandler handler, long from, string phone) =>
        handler.HandleMessageAsync(new Message
        {
            Chat = new Chat { Id = from },
            From = new User { Id = from },
            Contact = new Contact { PhoneNumber = phone, UserId = from }
        });

    // ── "account not found" ─────────────────────────────────────────────────────

    /// <summary>
    /// <c>/start</c> is the registration command. Answering it with "your account was not
    /// found, please register with the start command" tells somebody to do the thing they are
    /// doing, and it lands as the reply to their very first message — it reads as a rejection.
    /// </summary>
    [Fact]
    public async Task StartFromAStrangerIsNotAnsweredWithAccountNotFound()
    {
        var handler = Build();

        await SayAsync(handler, "/start", from: StrangerTelegramId);

        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("حساب شما پیدا نشد"));
    }

    /// <summary>But it still registers them — the message was all that changed.</summary>
    [Fact]
    public async Task StartFromAStrangerStillRegistersThem()
    {
        var handler = Build();

        await SayAsync(handler, "/start", from: StrangerTelegramId);

        Assert.Equal(StrangerTelegramId, Assert.Single(_usersApi.Registrations).TelegramId);
    }

    /// <summary>
    /// Anything else from somebody unknown still gets the prompt — they have no other way to
    /// learn what to do.
    /// </summary>
    [Fact]
    public async Task AnyOtherMessageFromAStrangerStillGetsThePrompt()
    {
        var handler = Build();

        await SayAsync(handler, "سلام", from: StrangerTelegramId);

        Assert.Contains(_messenger.Texts, t => t.Contains("حساب شما پیدا نشد"));
    }

    /// <summary>
    /// The prompt has to name the command as <c>/start</c>, written plainly. Telegram turns
    /// that into something tappable; "با دستور شروع ثبت‌نام کنید" left the person to guess what
    /// the command was and gave them nothing to press.
    /// </summary>
    [Fact]
    public async Task ThePromptNamesTheCommandSoTelegramCanMakeItTappable()
    {
        var handler = Build();

        await SayAsync(handler, "سلام", from: StrangerTelegramId);

        var prompt = Assert.Single(_messenger.Texts, t => t.Contains("حساب شما پیدا نشد"));
        Assert.Contains("/start", prompt);
    }

    // ── the owner does not approve themselves ───────────────────────────────────

    /// <summary>
    /// A configured owner is approved on the spot. Their authority comes from the configuration
    /// file; there is nobody else on an empty system to tick the box, and the exemption at the
    /// message gate alone would leave the stored row saying Pending forever while the bot
    /// behaved otherwise.
    /// </summary>
    [Fact]
    public async Task AConfiguredOwnerIsApprovedWhenTheyShareTheirNumber()
    {
        _usersApi.User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = OwnerTelegramId,
            FirstName = "محمد",
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser
        };

        var handler = Build(owners: [OwnerTelegramId]);

        await ShareContactAsync(handler, OwnerTelegramId, "09209698569");

        var change = Assert.Single(_usersApi.StatusChanges);
        Assert.Equal(OwnerTelegramId, change.TelegramId);
        Assert.Equal(UserStatus.Approved, change.NewStatus);
    }

    /// <summary>
    /// The owner is also <b>given</b> the Admin role, not merely treated as one.
    ///
    /// <para>
    /// Being an operator by configuration is enough to reach the commands, but not enough to be
    /// the shop: publishing a quote makes that account the counterparty of every fill against
    /// it, and only an Admin is exempt from the balance check that would otherwise refuse those
    /// trades. Left at RegularUser, the owner would see a menu they could not use.
    /// </para>
    ///
    /// <para>
    /// This is what removes the last manual step from a first deployment: configure one Telegram
    /// id, register, and the shop exists.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AConfiguredOwnerIsGivenTheAdminRole()
    {
        var ownerId = Guid.NewGuid();

        _usersApi.User = new UserDto
        {
            Id = ownerId,
            TelegramId = OwnerTelegramId,
            FirstName = "محمد",
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser
        };

        var handler = Build(owners: [OwnerTelegramId]);

        await ShareContactAsync(handler, OwnerTelegramId, "09209698569");

        var change = Assert.Single(_usersApi.RoleChanges);
        Assert.Equal(ownerId, change.UserId);
        Assert.Equal(UserRole.Admin, change.NewRole);
    }

    /// <summary>An owner who already holds the role is not promoted a second time.</summary>
    [Fact]
    public async Task AnOwnerWhoIsAlreadyAnAdminIsNotPromotedAgain()
    {
        _usersApi.User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = OwnerTelegramId,
            FirstName = "محمد",
            Status = UserStatus.Pending,
            Role = UserRole.Admin
        };

        var handler = Build(owners: [OwnerTelegramId]);

        await ShareContactAsync(handler, OwnerTelegramId, "09209698569");

        Assert.Empty(_usersApi.RoleChanges);
    }

    /// <summary>Somebody who is not an owner gets neither the approval nor the role.</summary>
    [Fact]
    public async Task AnOrdinaryRegistrationIsNotGivenTheAdminRole()
    {
        _usersApi.User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = StrangerTelegramId,
            FirstName = "کاربر",
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser
        };

        var handler = Build(owners: [OwnerTelegramId]);

        await ShareContactAsync(handler, StrangerTelegramId, "09121234567");

        Assert.Empty(_usersApi.RoleChanges);
    }

    /// <summary>Somebody who is not an owner is not approved for them.</summary>
    [Fact]
    public async Task AnOrdinaryRegistrationIsNotApprovedAutomatically()
    {
        _usersApi.User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = StrangerTelegramId,
            FirstName = "کاربر",
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser
        };

        var handler = Build(owners: [OwnerTelegramId]);

        await ShareContactAsync(handler, StrangerTelegramId, "09121234567");

        Assert.Empty(_usersApi.StatusChanges);
    }

    // ── the membership card ─────────────────────────────────────────────────────

    private static UserDto Applicant(string? first = "محمد", string? last = null) => new()
    {
        Id = Guid.NewGuid(),
        TelegramId = OwnerTelegramId,
        FirstName = first,
        LastName = last,
        Username = "KardanMohammad",
        PhoneNumber = "09209698569",
        Status = UserStatus.Pending,
        CreatedAt = new DateTime(2026, 7, 31, 8, 36, 0, DateTimeKind.Utc)
    };

    /// <summary>
    /// Nobody is ever asked to approve themselves. An administrator who registers is in the
    /// administrator list, so the card about their own account came back to them with buttons
    /// deciding their own fate — which is what happened on the first run of the wiped database.
    /// </summary>
    [Fact]
    public async Task TheCardIsNeverSentToThePersonItIsAbout()
    {
        var applicant = Applicant();

        await _messenger.SendApproveOrRejectUserToAdminsKeyboard(
            new[] { applicant.TelegramId, StrangerTelegramId }, applicant);

        Assert.DoesNotContain(_messenger.Sent, m => m.ChatId == applicant.TelegramId);
        Assert.Contains(_messenger.Sent, m => m.ChatId == StrangerTelegramId);
    }

    /// <summary>
    /// Who receives the card must come from the product's own idea of an operator — Admin or
    /// SuperAdmin in the database, or a configured owner — not from asking Telegram who
    /// administers one hard-coded group. A newly appointed admin is in neither Telegram group
    /// nor the old lookup's world, so they never saw the card even though "ت"/"ر" already let
    /// them act on it.
    ///
    /// <para>
    /// <c>botClient: null!</c> in <see cref="Build"/> is the proof: the old code reached for the
    /// Telegram group lookup on that null client, threw, and the exception was swallowed by the
    /// handler's own catch-all — silently sending nobody the card. This test passing at all
    /// means the new path no longer touches the bot client for this decision.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheCardGoesToDatabaseOperatorsAndConfiguredOwners_NotATelegramGroup()
    {
        const long DatabaseAdminId = 111222333;

        _usersApi.User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = StrangerTelegramId,
            FirstName = "کاربر",
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser
        };
        _usersApi.OperatorTelegramIds = [DatabaseAdminId];

        var handler = Build(owners: [OwnerTelegramId]);

        await ShareContactAsync(handler, StrangerTelegramId, "09121234567");

        Assert.Contains(_messenger.Sent, m => m.ChatId == DatabaseAdminId);
        Assert.Contains(_messenger.Sent, m => m.ChatId == OwnerTelegramId);
    }

    /// <summary>
    /// Every label is Persian. Half the card used to be English — "Telegram ID", "Username",
    /// "Phone" beside "نام" and "ثبت‌نام" — and a left-to-right label inside a right-to-left
    /// message breaks the direction of the line, so the card rendered scrambled rather than
    /// merely inconsistent.
    /// </summary>
    [Fact]
    public async Task EveryLabelOnTheCardIsPersian()
    {
        await _messenger.SendApproveOrRejectUserToAdminsKeyboard(
            new[] { StrangerTelegramId }, Applicant());

        var card = Assert.Single(_messenger.Texts);

        Assert.Contains("نام:", card);
        Assert.Contains("شمارهٔ تلفن:", card);
        Assert.Contains("نام کاربری:", card);
        Assert.Contains("شناسهٔ تلگرام:", card);
        Assert.Contains("تاریخ ثبت‌نام:", card);

        Assert.DoesNotContain("Telegram ID", card);
        Assert.DoesNotContain("Username", card);
        Assert.DoesNotContain("Phone", card);
    }

    /// <summary>
    /// Numbers appear in Persian digits, as they do everywhere else the bot shows a number.
    /// </summary>
    [Fact]
    public async Task TheNumbersOnTheCardAreInPersianDigits()
    {
        await _messenger.SendApproveOrRejectUserToAdminsKeyboard(
            new[] { StrangerTelegramId }, Applicant());

        var card = Assert.Single(_messenger.Texts);

        Assert.Contains("۰۹۲۰۹۶۹۸۵۶۹", card);
        Assert.Contains("۶۳۸۹۴۴۹۳۰۸", card);

        // Ordinal on purpose. xUnit's default string comparison is culture-sensitive, and a
        // linguistic comparison treats U+06F0-U+06F9 as equal to ASCII 0-9 — so the negative
        // assertion below passes for the wrong reason without it, reporting Latin digits in a
        // card that provably contains none. Verified by dumping the code points.
        Assert.DoesNotContain("09209698569", card, StringComparison.Ordinal);
    }

    /// <summary>
    /// A missing surname must not become a dash. <c>EscapeHtml</c> turns an empty value into
    /// "-", and the two parts were concatenated unconditionally, so somebody with only a first
    /// name was announced as "Mohammad -".
    /// </summary>
    [Fact]
    public async Task AMissingSurnameDoesNotLeaveADanglingDash()
    {
        await _messenger.SendApproveOrRejectUserToAdminsKeyboard(
            new[] { StrangerTelegramId }, Applicant(first: "محمد", last: null));

        var card = Assert.Single(_messenger.Texts);

        Assert.Contains("نام: محمد\n", card);
    }

    [Fact]
    public async Task BothPartsOfAFullNameAreShown()
    {
        await _messenger.SendApproveOrRejectUserToAdminsKeyboard(
            new[] { StrangerTelegramId }, Applicant(first: "محمد", last: "کاردان"));

        Assert.Contains("نام: محمد کاردان\n", Assert.Single(_messenger.Texts));
    }

    /// <summary>Somebody with no name at all still produces a readable card.</summary>
    [Fact]
    public async Task AnApplicantWithNoNameAtAllIsShownAsADash()
    {
        await _messenger.SendApproveOrRejectUserToAdminsKeyboard(
            new[] { StrangerTelegramId }, Applicant(first: null, last: null));

        Assert.Contains("نام: -\n", Assert.Single(_messenger.Texts));
    }
}
