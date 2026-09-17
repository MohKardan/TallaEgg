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
/// </remarks>
public class MenuBeforeApprovalTests
{
    private const long OwnerTelegramId = 6389449308;
    private const long CustomerTelegramId = 262176247;
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

    /// <summary>
    /// A main menu, the customer's or the admin's: both carry «📊 حسابداری», and nothing else the
    /// bot sends to a customer does.
    /// </summary>
    private static bool IsMainMenu(FakeBotMessenger.SentMessage message) =>
        message.ReplyMarkup is ReplyKeyboardMarkup keyboard
        && keyboard.Keyboard.SelectMany(row => row).Any(button => button.Text == BotBtns.BtnAccounting);

    private bool MenuWasSentTo(long chatId) =>
        _messenger.Sent.Any(m => m.ChatId == chatId && IsMainMenu(m));

    // ── registration ────────────────────────────────────────────────────────────

    /// <summary>The defect: no button that can only refuse.</summary>
    [Fact]
    public async Task APendingCustomerWhoSharesTheirPhone_IsShownNoMenu()
    {
        _usersApi.User = Person(CustomerTelegramId, phone: null, UserRole.RegularUser, UserStatus.Pending);

        await ShareContactAsync(Build(), CustomerTelegramId, CustomerPhone);

        Assert.False(MenuWasSentTo(CustomerTelegramId), "A pending customer was shown the menu.");
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
    public async Task TheOwnerWhoSharesTheirPhone_IsStillShownTheMenu()
    {
        _usersApi.User = Person(OwnerTelegramId, phone: null, UserRole.RegularUser, UserStatus.Pending);

        await ShareContactAsync(Build(owners: [OwnerTelegramId]), OwnerTelegramId, "09209698569");

        Assert.True(MenuWasSentTo(OwnerTelegramId), "The owner was not shown the menu.");
    }

    // ── approval brings the menu ────────────────────────────────────────────────

    [Fact]
    public async Task ACustomerApprovedWithTheCommand_ReceivesTheMenu()
    {
        _usersApi.User = Person(OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        await SayAsync(Build(), $"ت {CustomerPhone}", from: OwnerTelegramId);

        Assert.True(MenuWasSentTo(CustomerTelegramId), "The approved customer was left without a menu.");
    }

    [Fact]
    public async Task ACustomerApprovedWithTheButton_ReceivesTheMenu()
    {
        _usersApi.User = Person(OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);

        await TapAsync(Build(owners: [OwnerTelegramId]), $"approve_{CustomerTelegramId}", from: OwnerTelegramId);

        Assert.True(MenuWasSentTo(CustomerTelegramId), "The approved customer was left without a menu.");
    }

    /// <summary>The menu follows the approval notice, so the customer reads why before they tap.</summary>
    [Fact]
    public async Task TheMenuArrivesAfterTheApprovalNotice()
    {
        _usersApi.User = Person(OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        await SayAsync(Build(), $"ت {CustomerPhone}", from: OwnerTelegramId);

        var toCustomer = _messenger.Sent.Where(m => m.ChatId == CustomerTelegramId).ToList();
        var notice = toCustomer.FindIndex(m => m.Text == BotMsgs.MsgUserApproved);
        var menu = toCustomer.FindIndex(IsMainMenu);

        Assert.True(notice >= 0, "No approval notice.");
        Assert.True(menu > notice, "The menu did not follow the approval notice.");
    }

    // ── rejection does not ──────────────────────────────────────────────────────

    [Fact]
    public async Task ACustomerRejectedWithTheCommand_ReceivesNoMenu()
    {
        _usersApi.User = Person(OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);
        _usersApi.UsersByPhone[CustomerPhone] =
            Person(CustomerTelegramId, CustomerPhone, UserRole.RegularUser, UserStatus.Pending);

        await SayAsync(Build(), $"ر {CustomerPhone}", from: OwnerTelegramId);

        Assert.False(MenuWasSentTo(CustomerTelegramId), "A rejected customer was shown the menu.");
    }

    [Fact]
    public async Task ACustomerRejectedWithTheButton_ReceivesNoMenu()
    {
        _usersApi.User = Person(OwnerTelegramId, "09209698569", UserRole.Admin, UserStatus.Approved);

        await TapAsync(Build(owners: [OwnerTelegramId]), $"reject_{CustomerTelegramId}", from: OwnerTelegramId);

        Assert.False(MenuWasSentTo(CustomerTelegramId), "A rejected customer was shown the menu.");
    }
}
