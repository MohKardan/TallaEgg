using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TallaEgg.AllServices.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A customer sees the menu once their account can use it, and not before (issue #291).
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong:</b> right after a new customer shared their phone number, the bot said
/// «حساب شما در انتظار تایید مدیر است» and then showed the full customer menu. Every button on it
/// answered «حساب کاربری شما هنوز فعال نشده است». It was the first screen a prospect saw in a demo,
/// and it read as broken rather than pending.
/// </para>
/// <para>
/// Removing that menu has a consequence these tests also pin: the menu shown at registration was
/// the keyboard an approved customer went on using, because the approval notice carries none. So
/// the menu now arrives with the approval instead — on both approval paths, the admin's button
/// and the <c>ت</c> command — and a rejection still brings no menu.
/// </para>
/// <para>
/// Sending a menu on approval raised the stakes of three things the button path never checked,
/// and the review of the change found them: it notified the customer even when the status update
/// failed, it notified them again on every repeated tap, and choosing the menu by looking the
/// customer up again turned a momentary lookup failure into «حساب شما پیدا نشد». Each has a test
/// below.
/// </para>
/// </remarks>
public class MenuBeforeApprovalTests
{
    private const long OwnerTelegramId = 6389449308;
    private const long CustomerTelegramId = 262176247;
    private const string OwnerPhone = "09209698569";
    private const string CustomerPhone = "09158527483";

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();

    private static UserDto Person(long telegramId, string? phone, UserRole role, UserStatus status) => new()
    {
        Id = Guid.NewGuid(),
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
        versionService: new FakeVersionService(),
        ownerTelegramIds: owners);

    private Task ShareContactAsync(BotHandler handler, long from, string phone) =>
        handler.HandleMessageAsync(new Message
        {
            Chat = new Chat { Id = from },
            From = new User { Id = from },
            Contact = new Contact { PhoneNumber = phone, UserId = from }
        });

    private Task SayAsync(BotHandler handler, string text, long from) =>
        handler.HandleMessageAsync(new Message
        {
            Text = text,
            Chat = new Chat { Id = from },
            From = new User { Id = from }
        });

    private Task TapAsync(BotHandler handler, string callbackData, long from) =>
        handler.HandleCallbackQueryAsync(new CallbackQuery
        {
            Id = "cb-1",
            Data = callbackData,
            From = new User { Id = from },
            Message = new Message { Id = 1, Text = "…", Chat = new Chat { Id = from } }
        });

    /// <summary>The owner is an approved Admin; the customer is pending and reachable by id.</summary>
    private void OwnerAndPendingCustomer()
    {
        var owner = Person(OwnerTelegramId, OwnerPhone, UserRole.Admin, UserStatus.Approved);
        var customer = Person(CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        _usersApi.User = owner;
        _usersApi.UsersByTelegramId[OwnerTelegramId] = owner;
        _usersApi.UsersByTelegramId[CustomerTelegramId] = customer;
        _usersApi.UsersByPhone[CustomerPhone] = customer;
    }

    private static IEnumerable<string> ButtonsOf(FakeBotMessenger.SentMessage message) =>
        message.ReplyMarkup is ReplyKeyboardMarkup keyboard
            ? keyboard.Keyboard.SelectMany(row => row).Select(button => button.Text)
            : [];

    /// <summary>The customer's menu: it offers «💹 دریافت مظنه».</summary>
    private static bool IsCustomerMenu(FakeBotMessenger.SentMessage m) => ButtonsOf(m).Contains(BotBtns.BtnSpotMarket);

    /// <summary>The admin's menu: it offers «💹 اعلام مظنه».</summary>
    private static bool IsAdminMenu(FakeBotMessenger.SentMessage m) => ButtonsOf(m).Contains(BotBtns.BtnSpotSubmitPrice);

    private List<FakeBotMessenger.SentMessage> SentTo(long chatId) =>
        _messenger.Sent.Where(m => m.ChatId == chatId).ToList();

    private bool AnyMenuSentTo(long chatId) => SentTo(chatId).Any(m => IsCustomerMenu(m) || IsAdminMenu(m));

    // ── registration ────────────────────────────────────────────────────────────

    /// <summary>The defect: no button that can only refuse.</summary>
    [Fact]
    public async Task APendingCustomerWhoSharesTheirPhone_IsShownNoMenu()
    {
        _usersApi.User = Person(CustomerTelegramId, phone: null, UserRole.RegularUser, UserStatus.Pending);

        await ShareContactAsync(Build(), CustomerTelegramId, CustomerPhone);

        Assert.False(AnyMenuSentTo(CustomerTelegramId), "A pending customer was shown a menu.");
    }

    /// <summary>They are still told the number was taken and that approval comes next.</summary>
    [Fact]
    public async Task APendingCustomerWhoSharesTheirPhone_IsStillToldTheyAreWaitingForApproval()
    {
        _usersApi.User = Person(CustomerTelegramId, phone: null, UserRole.RegularUser, UserStatus.Pending);

        await ShareContactAsync(Build(), CustomerTelegramId, CustomerPhone);

        Assert.Contains(BotMsgs.MsgPhoneSuccess, _messenger.Texts);
    }

    /// <summary>The configured owner is approved in the same step, so they still get their menu.</summary>
    [Fact]
    public async Task TheOwnerWhoSharesTheirPhone_IsStillShownTheAdminMenu()
    {
        _usersApi.User = Person(OwnerTelegramId, phone: null, UserRole.RegularUser, UserStatus.Pending);

        await ShareContactAsync(Build(owners: [OwnerTelegramId]), OwnerTelegramId, OwnerPhone);

        Assert.Contains(SentTo(OwnerTelegramId), IsAdminMenu);
    }

    // ── approval brings the customer's menu ─────────────────────────────────────

    [Fact]
    public async Task ACustomerApprovedWithTheCommand_ReceivesTheCustomerMenu()
    {
        OwnerAndPendingCustomer();

        await SayAsync(Build(), $"ت {CustomerPhone}", from: OwnerTelegramId);

        Assert.Contains(SentTo(CustomerTelegramId), IsCustomerMenu);
        Assert.DoesNotContain(SentTo(CustomerTelegramId), IsAdminMenu);
    }

    [Fact]
    public async Task ACustomerApprovedWithTheButton_ReceivesTheCustomerMenu()
    {
        OwnerAndPendingCustomer();

        await TapAsync(Build(owners: [OwnerTelegramId]), $"approve_{CustomerTelegramId}", from: OwnerTelegramId);

        Assert.Contains(SentTo(CustomerTelegramId), IsCustomerMenu);
        Assert.DoesNotContain(SentTo(CustomerTelegramId), IsAdminMenu);
    }

    /// <summary>The menu follows the approval notice, so the customer reads why before they tap.</summary>
    [Fact]
    public async Task TheMenuArrivesAfterTheApprovalNotice()
    {
        OwnerAndPendingCustomer();

        await SayAsync(Build(), $"ت {CustomerPhone}", from: OwnerTelegramId);

        var toCustomer = SentTo(CustomerTelegramId);
        var notice = toCustomer.FindIndex(m => m.Text == BotMsgs.MsgUserApproved);
        var menu = toCustomer.FindIndex(IsCustomerMenu);

        Assert.True(notice >= 0, "No approval notice.");
        Assert.True(menu > notice, "The menu did not follow the approval notice.");
    }

    // ── the button path's failures ──────────────────────────────────────────────

    /// <summary>
    /// If the status update fails the account is still pending, so telling the customer they are
    /// approved and handing them a menu that refuses them would be #291 again. The admin is told.
    /// </summary>
    [Fact]
    public async Task WhenTheButtonsStatusUpdateFails_TheCustomerIsNeitherNotifiedNorShownAMenu()
    {
        OwnerAndPendingCustomer();
        _usersApi.StatusChangeResult = TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("سرویس کاربران در دسترس نیست");

        await TapAsync(Build(owners: [OwnerTelegramId]), $"approve_{CustomerTelegramId}", from: OwnerTelegramId);

        Assert.Empty(SentTo(CustomerTelegramId));
        Assert.Contains(_messenger.Texts, t => t.Contains("سرویس کاربران در دسترس نیست"));
    }

    /// <summary>
    /// The approval card goes to every operator, and a button can be tapped twice. A customer who
    /// is already approved must not get the notice and the menu again.
    /// </summary>
    [Fact]
    public async Task TappingApproveForAnAlreadyApprovedCustomer_SendsThemNothing()
    {
        OwnerAndPendingCustomer();
        _usersApi.UsersByTelegramId[CustomerTelegramId] =
            Person(CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Approved);

        await TapAsync(Build(owners: [OwnerTelegramId]), $"approve_{CustomerTelegramId}", from: OwnerTelegramId);

        Assert.Empty(SentTo(CustomerTelegramId));
        Assert.Empty(_usersApi.StatusChanges);
    }

    /// <summary>
    /// Choosing the menu must not depend on looking the customer up again after the approval went
    /// through. That lookup answering nothing used to mean «حساب شما پیدا نشد» sent to a customer
    /// who had just been approved.
    /// </summary>
    [Fact]
    public async Task WhenTheCustomerCannotBeLookedUpAfterApproval_TheyStillGetTheirMenuAndNoNotFound()
    {
        OwnerAndPendingCustomer();
        _usersApi.UsersByTelegramId.Remove(CustomerTelegramId);

        await TapAsync(Build(owners: [OwnerTelegramId]), $"approve_{CustomerTelegramId}", from: OwnerTelegramId);

        Assert.Contains(SentTo(CustomerTelegramId), m => m.Text == BotMsgs.MsgUserApproved);
        Assert.Contains(SentTo(CustomerTelegramId), IsCustomerMenu);
        Assert.DoesNotContain(SentTo(CustomerTelegramId), m => m.Text == BotMsgs.MsgAccountNotFound);
    }

    // ── rejection does not ──────────────────────────────────────────────────────

    [Fact]
    public async Task ACustomerRejectedWithTheCommand_ReceivesNoMenu()
    {
        OwnerAndPendingCustomer();

        await SayAsync(Build(), $"ر {CustomerPhone}", from: OwnerTelegramId);

        Assert.False(AnyMenuSentTo(CustomerTelegramId), "A rejected customer was shown a menu.");
    }

    [Fact]
    public async Task ACustomerRejectedWithTheButton_ReceivesNoMenu()
    {
        OwnerAndPendingCustomer();

        await TapAsync(Build(owners: [OwnerTelegramId]), $"reject_{CustomerTelegramId}", from: OwnerTelegramId);

        Assert.False(AnyMenuSentTo(CustomerTelegramId), "A rejected customer was shown a menu.");
    }
}
