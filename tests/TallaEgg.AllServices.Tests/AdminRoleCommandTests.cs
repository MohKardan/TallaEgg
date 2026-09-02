using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using TallaEgg.AllServices.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// <c>ن [شمارهٔ تلفن] [نقش]</c> — granting and removing authority from inside the bot.
///
/// <para>
/// <b>Why this command exists.</b> The Users service has exposed
/// <c>POST /api/user/update-role</c> since the beginning and nothing ever called it, so a role
/// could only be changed by running SQL against the database by hand. That is why the operator
/// row in the current database was created manually, and it is why a newly deployed instance
/// has no operator at all: the first person to register is an ordinary user, administrative
/// commands are only reachable by an operator, and there is no third path.
/// </para>
///
/// <para>
/// <b>Why the owner list exists.</b> A role command alone does not break that deadlock — someone
/// has to be able to run it first. <c>BotSettings:OwnerTelegramIds</c> is that first step:
/// authority from configuration, which is readable when the <c>Users</c> table is empty. The
/// seeded SuperAdmin is not a substitute; it is created with <c>TelegramId = 0</c>, so no human
/// can ever be signed in as it.
/// </para>
///
/// <para>
/// These tests drive the real handler through <c>HandleMessageAsync</c>, so they cover the
/// authority check, the parse, and the call to the Users service together — which is where the
/// mistakes would be.
/// </para>
/// </summary>
public class AdminRoleCommandTests
{
    private const long AdminTelegramId = 5001;
    private const long TargetTelegramId = 6002;
    private const long OwnerTelegramId = 7003;

    private const string TargetPhone = "09121234567";

    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();

    private static UserDto Person(Guid id, long telegramId, string phone, UserRole role) => new()
    {
        Id = id,
        TelegramId = telegramId,
        FirstName = "کاربر",
        PhoneNumber = phone,
        Status = UserStatus.Approved,
        Role = role
    };

    /// <summary>
    /// Builds the handler with a given caller. <paramref name="owners"/> is what the deployment
    /// would put in configuration.
    /// </summary>
    private BotHandler Build(UserRole callerRole, IEnumerable<long>? owners = null)
    {
        _usersApi.User = Person(AdminId, AdminTelegramId, "09000000000", callerRole);
        _usersApi.UsersByPhone[TargetPhone] = Person(TargetId, TargetTelegramId, TargetPhone, UserRole.RegularUser);

        return new BotHandler(
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
    }

    private static Message From(long telegramId, string text) => new()
    {
        Text = text,
        Chat = new Chat { Id = telegramId },
        From = new User { Id = telegramId }
    };

    private Task SayAsync(BotHandler handler, string text, long from = AdminTelegramId) =>
        handler.HandleMessageAsync(From(from, text));

    // ── the happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AnAdminCanPromoteAnotherUser()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, $"ن {TargetPhone} مدیر");

        var change = Assert.Single(_usersApi.RoleChanges);
        Assert.Equal(TargetId, change.UserId);
        Assert.Equal(UserRole.Admin, change.NewRole);
    }

    [Fact]
    public async Task ANumericRoleWorksJustAsWell()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, $"ن {TargetPhone} 1");

        Assert.Equal(UserRole.Accountant, Assert.Single(_usersApi.RoleChanges).NewRole);
    }

    /// <summary>
    /// The confirmation must carry the target's id. <c>Matching:MarketMakerUserId</c> names one
    /// specific row, and on a database created from empty that id is generated at registration —
    /// nobody can know it in advance. Without this line the deployment step "point the config at
    /// the dealer" needs a database query.
    /// </summary>
    [Fact]
    public async Task TheConfirmationCarriesTheUserIdNeededByTheDealerConfiguration()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, $"ن {TargetPhone} مدیر");

        Assert.Contains(_messenger.Texts, t => t.Contains(TargetId.ToString()));
    }

    /// <summary>
    /// Their menu changes on their next message. An unexplained change in what the bot offers
    /// reads as a fault, so the change is announced.
    /// </summary>
    [Fact]
    public async Task TheAffectedUserIsToldAboutIt()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, $"ن {TargetPhone} حسابدار");

        var notice = Assert.Single(_messenger.Sent, m => m.ChatId == TargetTelegramId);
        Assert.Contains("حسابدار", notice.Text);
    }

    [Fact]
    public async Task BothTheOldAndTheNewRoleAreReportedBack()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, $"ن {TargetPhone} مدیر");

        var confirmation = Assert.Single(_messenger.Sent, m => m.ChatId == AdminTelegramId);
        Assert.Contains("کاربر عادی", confirmation.Text);   // what they were
        Assert.Contains("مدیر", confirmation.Text);          // what they are now
    }

    // ── who may run it ──────────────────────────────────────────────────────────

    /// <summary>
    /// The whole point of the guard. An ordinary user typing the command must not reach the
    /// Users service at all — not be told "no" after the call.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryUserCannotChangeAnybodysRole()
    {
        var handler = Build(UserRole.RegularUser);

        await SayAsync(handler, $"ن {TargetPhone} مدیر");

        Assert.Empty(_usersApi.RoleChanges);
    }

    /// <summary>
    /// SuperAdmin is strictly more privileged than Admin, yet the gate was written
    /// <c>role == Admin</c> and excluded it. Before this fix, granting someone the higher of the
    /// two roles took their administrative access away — and now that roles can be changed in one
    /// message, that is a mistake anyone could make.
    /// </summary>
    [Fact]
    public async Task ASuperAdminIsAnOperatorToo()
    {
        var handler = Build(UserRole.SuperAdmin);

        await SayAsync(handler, $"ن {TargetPhone} مدیر");

        Assert.Single(_usersApi.RoleChanges);
    }

    /// <summary>
    /// The bootstrap. This caller's stored role is <see cref="UserRole.RegularUser"/> — exactly
    /// what the first person to register on a new deployment gets — and they can still assign
    /// roles, because configuration says who they are. Without this, an empty database has no
    /// operator and no way to appoint one.
    /// </summary>
    [Fact]
    public async Task AConfiguredOwnerCanAssignRolesWithNoAdminRowAnywhere()
    {
        var handler = Build(UserRole.RegularUser, owners: [OwnerTelegramId]);
        _usersApi.User = Person(AdminId, OwnerTelegramId, "09000000000", UserRole.RegularUser);

        await SayAsync(handler, $"ن {TargetPhone} مدیر", from: OwnerTelegramId);

        Assert.Equal(UserRole.Admin, Assert.Single(_usersApi.RoleChanges).NewRole);
    }

    /// <summary>Being on the list is per-id, not a switch that opens the command to everyone.</summary>
    [Fact]
    public async Task SomeoneNotOnTheOwnerListIsStillRefused()
    {
        var handler = Build(UserRole.RegularUser, owners: [OwnerTelegramId]);

        await SayAsync(handler, $"ن {TargetPhone} مدیر");   // sent by AdminTelegramId

        Assert.Empty(_usersApi.RoleChanges);
    }

    // ── refusals ────────────────────────────────────────────────────────────────

    /// <summary>
    /// If the only operator could demote themselves, one message would remove administrative
    /// access from the product entirely: the command that restores it would no longer be
    /// reachable, and recovery would need a configuration edit and a restart.
    /// </summary>
    [Fact]
    public async Task AnOperatorCannotChangeTheirOwnRole()
    {
        var handler = Build(UserRole.Admin);
        _usersApi.UsersByPhone["09000000000"] = _usersApi.User!;

        await SayAsync(handler, "ن 09000000000 کاربر عادی");

        Assert.Empty(_usersApi.RoleChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains("نقش خودتان"));
    }

    [Fact]
    public async Task AnUnrecognisedRoleIsRefusedWithoutCallingTheService()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, $"ن {TargetPhone} رئیس");

        Assert.Empty(_usersApi.RoleChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains("شناسایی نشد"));
    }

    [Fact]
    public async Task AMalformedCommandIsAnsweredWithTheFormat()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, "ن مدیر");

        Assert.Empty(_usersApi.RoleChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains("قالب"));
    }

    [Fact]
    public async Task AnUnknownPhoneNumberIsReported()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, "ن 09999999999 مدیر");

        Assert.Empty(_usersApi.RoleChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains(BotMsgs.MsgAdminUserNotFound));
    }

    /// <summary>Asking for the role somebody already has is not an error, and not a write.</summary>
    [Fact]
    public async Task AskingForTheRoleTheyAlreadyHaveChangesNothing()
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, $"ن {TargetPhone} کاربر عادی");

        Assert.Empty(_usersApi.RoleChanges);
        Assert.Contains(_messenger.Texts, t => t.Contains("تغییری لازم نبود"));
    }

    /// <summary>
    /// When the Users service refuses, its own message is shown. A role change is rare and
    /// consequential; "failed" alone leaves the operator unable to tell an unreachable service
    /// from a rejected request at the one moment they need to know.
    /// </summary>
    [Fact]
    public async Task AFailureFromTheUsersServiceIsReportedWithItsReason()
    {
        var handler = Build(UserRole.Admin);
        _usersApi.RoleChangeResult = (false, "کاربر یافت نشد.");

        await SayAsync(handler, $"ن {TargetPhone} مدیر");

        Assert.Contains(_messenger.Texts, t => t.Contains("کاربر یافت نشد."));
        Assert.DoesNotContain(_messenger.Sent, m => m.ChatId == TargetTelegramId);
    }

    // ── not swallowing ordinary messages ────────────────────────────────────────

    /// <summary>
    /// The prefix requires a following space, so ordinary words beginning with "ن" fall through
    /// to the normal menu instead of being answered with a format error.
    /// </summary>
    [Theory]
    [InlineData("نمودار")]
    [InlineData("نرخ")]
    public async Task AWordStartingWithTheSameLetterIsNotTreatedAsThisCommand(string text)
    {
        var handler = Build(UserRole.Admin);

        await SayAsync(handler, text);

        Assert.Empty(_usersApi.RoleChanges);
        Assert.DoesNotContain(_messenger.Texts, t => t.Contains("قالب دستور درست نیست"));
    }
}
