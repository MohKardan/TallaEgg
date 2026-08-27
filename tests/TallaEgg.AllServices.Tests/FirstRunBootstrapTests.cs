using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.TelegramBot.Infrastructure.Options;
using Telegram.Bot.Types;
using TallaEgg.AllServices.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The first minutes of a brand-new deployment, against an empty database.
///
/// <para>
/// Adding a role command was not enough to make a fresh server usable. Three separate walls
/// stood in front of it, each invisible on the existing database because that database already
/// contains people:
/// </para>
///
/// <list type="number">
///   <item><description><b>Registration is refused.</b> A code that belongs to no user is
///   rejected, and the bot's fallback code did not match the one on the row that
///   <c>Users.Api</c> seeds — so on an empty database nobody could register at all.</description></item>
///   <item><description><b>New accounts are Pending</b>, and approval arrived only as a button
///   sent to administrators of a hard-coded Telegram group.</description></item>
///   <item><description><b>Operator commands sit behind the approval check</b>, so even a
///   configured owner was told their account was not active yet.</description></item>
/// </list>
///
/// <para>
/// Together those made the first run a closed loop: the person who is meant to appoint the
/// first administrator needed an approval only an administrator could give. These tests pin
/// each wall open.
/// </para>
/// </summary>
public class FirstRunBootstrapTests
{
    private const long OwnerTelegramId = 6389449308;
    private const long CustomerTelegramId = 262176247;
    private const string CustomerPhone = "09158527483";

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();

    private static UserDto Person(Guid id, long telegramId, string phone, UserRole role, UserStatus status) => new()
    {
        Id = id,
        TelegramId = telegramId,
        FirstName = "کاربر",
        PhoneNumber = phone,
        Status = status,
        Role = role
    };

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

    // ── wall 1: the bootstrap invitation code ───────────────────────────────────

    /// <summary>
    /// The bot's fallback code and the seeded administrator's code must be the same string, or
    /// the first registration on an empty database is refused with "کد دعوت معتبر نیست".
    ///
    /// <para>
    /// They were <c>"ADMIN2024"</c> and <c>"admin"</c>. Both are now the one constant, so this
    /// asserts the constant is what the options actually default to — the copy that used to
    /// drift.
    /// </para>
    /// </summary>
    [Fact]
    public void TheBotsFallbackInvitationCodeIsTheSeededAdministratorsCode()
    {
        Assert.Equal(BootstrapConstant.RootInvitationCode, new BotSettingsOptions().DefaultReferralCode);
    }

    /// <summary>
    /// A guard on the constant itself. <c>Users.Api</c> seeds this value into the database at
    /// startup, so changing it silently strands every deployment whose administrator row was
    /// created under the old value.
    /// </summary>
    [Fact]
    public void TheBootstrapCodeIsTheValueAlreadySeededInExistingDatabases()
    {
        Assert.Equal("admin", BootstrapConstant.RootInvitationCode);
        Assert.Equal(Guid.Parse("5564f136-b9fb-4719-b4dc-b0833fa24761"), BootstrapConstant.RootAdminUserId);
    }

    // ── wall 3: the approval gate ───────────────────────────────────────────────

    /// <summary>
    /// The owner has just registered, so they are Pending — that is what every new account is.
    /// Before the exemption they were answered "حساب کاربری شما هنوز فعال نشده است" and the
    /// command was never reached, which is precisely the deadlock: their approval could only
    /// come from an administrator, and appointing one is what they were trying to do.
    /// </summary>
    [Fact]
    public async Task APendingOwnerCanStillAppointTheFirstAdministrator()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.RegularUser, UserStatus.Pending);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Approved);

        var handler = Build(owners: [OwnerTelegramId]);

        await SayAsync(handler, $"ن {CustomerPhone} مدیر", from: OwnerTelegramId);

        Assert.Equal(UserRole.Admin, Assert.Single(_usersApi.RoleChanges).NewRole);
    }

    /// <summary>
    /// The exemption is for configured owners only. An ordinary Pending account must still be
    /// held at the gate — otherwise the approval step would mean nothing at all.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryPendingAccountIsStillHeldAtTheGate()
    {
        _usersApi.User = Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        var handler = Build(owners: [OwnerTelegramId]);

        await SayAsync(handler, "سلام", from: CustomerTelegramId);

        Assert.Contains(_messenger.Texts, t => t.Contains("فعال نشده"));
    }

    /// <summary>
    /// And an Admin who has been suspended stays suspended. Being an operator is not itself a
    /// way past the approval gate — only the configuration is.
    /// </summary>
    [Fact]
    public async Task ASuspendedAdministratorIsNotExempt()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Suspended);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Approved);

        var handler = Build(owners: []);   // not configured as an owner

        await SayAsync(handler, $"ن {CustomerPhone} مدیر", from: OwnerTelegramId);

        Assert.Empty(_usersApi.RoleChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains("فعال نشده"));
    }

    // ── wall 2: approving customers without the Telegram group ──────────────────

    /// <summary>
    /// Approval used to require a button delivered to the administrators of a hard-coded
    /// Telegram group. A newly deployed bot is not in that group, <c>GetChatAdministrators</c>
    /// throws, the catch-all in <c>HandleMessageAsync</c> swallows it, and the customer waits
    /// forever with nobody told. This route depends on nothing outside the product.
    /// </summary>
    [Fact]
    public async Task AnOperatorCanApproveACustomerWithACommand()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        var handler = Build();

        await SayAsync(handler, $"ت {CustomerPhone}", from: OwnerTelegramId);

        var change = Assert.Single(_usersApi.StatusChanges);
        Assert.Equal(CustomerTelegramId, change.TelegramId);
        Assert.Equal(UserStatus.Approved, change.NewStatus);
    }

    [Fact]
    public async Task AnOperatorCanRejectACustomerWithACommand()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        var handler = Build();

        await SayAsync(handler, $"ر {CustomerPhone}", from: OwnerTelegramId);

        Assert.Equal(UserStatus.Rejected, Assert.Single(_usersApi.StatusChanges).NewStatus);
    }

    /// <summary>The customer is told, since their access changes.</summary>
    [Fact]
    public async Task TheApprovedCustomerIsNotified()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        var handler = Build();

        await SayAsync(handler, $"ت {CustomerPhone}", from: OwnerTelegramId);

        Assert.Contains(_messenger.Sent, m => m.ChatId == CustomerTelegramId && m.Text == BotMsgs.MsgUserApproved);
    }

    [Fact]
    public async Task AnOrdinaryUserCannotApproveAnybody()
    {
        _usersApi.User = Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Approved);

        var handler = Build();

        await SayAsync(handler, "ت 09121234567", from: CustomerTelegramId);

        Assert.Empty(_usersApi.StatusChanges);
    }

    /// <summary>
    /// The seeded administrator row has <c>TelegramId = 0</c> — it owns the bootstrap invitation
    /// code and is not a person. The status endpoint is keyed on the Telegram id, so it must be
    /// refused rather than sent as a change against id zero.
    /// </summary>
    [Fact]
    public async Task AnAccountWithNoTelegramIdIsRefusedRatherThanSentAsZero()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone["09000000001"] =
            Person(BootstrapConstant.RootAdminUserId, 0, "09000000001", UserRole.SuperAdmin, UserStatus.Pending);

        var handler = Build();

        await SayAsync(handler, "ت 09000000001", from: OwnerTelegramId);

        Assert.Empty(_usersApi.StatusChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains("به تلگرام متصل نیست"));
    }

    [Fact]
    public async Task ApprovingSomebodyAlreadyApprovedChangesNothing()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Approved);

        var handler = Build();

        await SayAsync(handler, $"ت {CustomerPhone}", from: OwnerTelegramId);

        Assert.Empty(_usersApi.StatusChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains("تغییری لازم نبود"));
    }

    [Fact]
    public async Task AnUnknownPhoneNumberIsReported()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        var handler = Build();

        await SayAsync(handler, "ت 09999999999", from: OwnerTelegramId);

        Assert.Empty(_usersApi.StatusChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains(BotMsgs.MsgAdminUserNotFound));
    }

    /// <summary>
    /// The prefixes are single letters, so they must require a following space or ordinary
    /// Persian words would be swallowed as malformed commands.
    /// </summary>
    [Theory]
    [InlineData("تاریخچه")]
    [InlineData("راهنما")]
    [InlineData("تایید")]
    public async Task AWordStartingWithTheSameLetterIsNotTreatedAsACommand(string text)
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);

        var handler = Build();

        await SayAsync(handler, text, from: OwnerTelegramId);

        Assert.Empty(_usersApi.StatusChanges);
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("قالب دستور درست نیست"));
    }

    // ── the approve/reject buttons ──────────────────────────────────────────────

    private Task TapAsync(BotHandler handler, string callbackData, long from) =>
        handler.HandleCallbackQueryAsync(new CallbackQuery
        {
            Id = "cb-1",
            Data = callbackData,
            From = new User { Id = from },
            Message = new Message { Id = 1, Text = "…", Chat = new Chat { Id = from } }
        });

    /// <summary>
    /// The button path had no authorization check whatsoever. Callback data is client-side
    /// text — any Telegram client can send back <c>approve_&lt;id&gt;</c> whether or not it was
    /// ever shown a button — so this was an open way to activate an account. It survived
    /// unnoticed because the buttons are only ever delivered to group administrators, but who
    /// receives a button is not who can send its data.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryUserCannotApproveByReplayingTheCallbackData()
    {
        _usersApi.User = Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Approved);

        var handler = Build();

        await TapAsync(handler, $"approve_{CustomerTelegramId}", from: CustomerTelegramId);

        Assert.Empty(_usersApi.StatusChanges);
    }

    [Fact]
    public async Task AnOrdinaryUserCannotRejectByReplayingTheCallbackData()
    {
        _usersApi.User = Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Approved);

        var handler = Build();

        await TapAsync(handler, $"reject_{CustomerTelegramId}", from: CustomerTelegramId);

        Assert.Empty(_usersApi.StatusChanges);
    }

    /// <summary>
    /// The other direction: the guard must not have broken the button for the people it is for.
    /// </summary>
    [Fact]
    public async Task AnOperatorTappingTheButtonStillApproves()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);

        var handler = Build();

        await TapAsync(handler, $"approve_{CustomerTelegramId}", from: OwnerTelegramId);

        var change = Assert.Single(_usersApi.StatusChanges);
        Assert.Equal(CustomerTelegramId, change.TelegramId);
        Assert.Equal(UserStatus.Approved, change.NewStatus);
    }

    // ── the full first run, end to end ──────────────────────────────────────────

    /// <summary>
    /// The whole sequence a new deployment goes through, in order: the owner arrives Pending on
    /// an otherwise empty system, appoints the shop account as administrator, and approves a
    /// customer. Every step here was blocked by one of the three walls.
    /// </summary>
    [Fact]
    public async Task TheCompleteFirstRunSucceeds()
    {
        _usersApi.User = Person(OwnerId, OwnerTelegramId, "09209698569", UserRole.RegularUser, UserStatus.Pending);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerId, CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        var handler = Build(owners: [OwnerTelegramId]);

        await SayAsync(handler, $"ن {CustomerPhone} مدیر", from: OwnerTelegramId);
        await SayAsync(handler, $"ت {CustomerPhone}", from: OwnerTelegramId);

        Assert.Equal(UserRole.Admin, Assert.Single(_usersApi.RoleChanges).NewRole);
        Assert.Equal(UserStatus.Approved, Assert.Single(_usersApi.StatusChanges).NewStatus);

        // And the id needed by Matching:MarketMakerUserId came back in the reply.
        Assert.Contains(_messenger.Texts, t => t.Contains(CustomerId.ToString()));
    }
}
