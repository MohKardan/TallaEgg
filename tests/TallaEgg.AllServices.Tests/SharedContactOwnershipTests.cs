using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A customer may only register the phone number of the Telegram account they are writing from
/// (issue #303).
///
/// <para>
/// The "share phone" button sends the sender's own contact and nothing else, so the bot read
/// <c>message.Contact.PhoneNumber</c> and stored it. But Telegram also lets anyone attach a
/// contact from their address book, and that arrives in the same field. Somebody who knew a
/// customer's number could therefore register under it: the card the operator was asked to
/// approve showed that number beside the sender's own Telegram profile name, which the sender
/// chooses. Reproduced on the demo server before this fix.
/// </para>
///
/// <para>
/// The distinguishing mark is <c>Contact.UserId</c>: Telegram sets it to the sender's own id on
/// a button share, and to the other person's id — or leaves it out — on a card picked from the
/// address book.
/// </para>
/// </summary>
public class SharedContactOwnershipTests
{
    private const long CustomerTelegramId = 262176247;
    private const long ImpostorTelegramId = 555000111;
    private const string SomebodyElsesPhone = "09158527483";

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();

    private BotHandler Build()
    {
        // Registered, no phone number yet: the state in which the bot is waiting for a contact.
        var joining = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = ImpostorTelegramId,
            FirstName = "کاربر",
            PhoneNumber = null,
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser
        };

        _usersApi.User = joining;
        _usersApi.UsersByTelegramId[ImpostorTelegramId] = joining;
        _usersApi.UsersByTelegramId[CustomerTelegramId] = joining;

        return new BotHandler(
            NullLogger<BotHandler>.Instance,
            botClient: null!,
            messenger: _messenger,
            conversations: new InMemoryConversationStore(),
            orderApi: new FakeOrderApiClient(),
            usersApi: _usersApi,
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: new StubWalletApiClient(),
            versionService: new FakeVersionService());
    }

    /// <summary>
    /// <paramref name="contactOwner"/> is what Telegram puts in <c>Contact.UserId</c>: the
    /// sender's own id for a button share, somebody else's or null for an attached card.
    /// </summary>
    private Task SendContactAsync(BotHandler handler, long from, string phone, long? contactOwner) =>
        handler.HandleMessageAsync(new Message
        {
            Chat = new Chat { Id = from },
            From = new User { Id = from },
            Contact = new Contact { PhoneNumber = phone, UserId = contactOwner }
        });

    [Fact]
    public async Task SharingSomebodyElsesContactCard_DoesNotStoreThatNumber()
    {
        var handler = Build();

        await SendContactAsync(handler, from: ImpostorTelegramId, SomebodyElsesPhone, contactOwner: CustomerTelegramId);

        Assert.Empty(_usersApi.PhoneUpdates);
    }

    /// <summary>
    /// A contact carrying no user id at all — an address-book entry for someone who is not on
    /// Telegram, or a manually typed card. It is equally not proof of ownership.
    /// </summary>
    [Fact]
    public async Task SharingAContactWithNoUserId_DoesNotStoreThatNumber()
    {
        var handler = Build();

        await SendContactAsync(handler, from: ImpostorTelegramId, SomebodyElsesPhone, contactOwner: null);

        Assert.Empty(_usersApi.PhoneUpdates);
    }

    /// <summary>The refusal has to say what to do instead, or the customer is simply stuck.</summary>
    [Fact]
    public async Task SharingSomebodyElsesContactCard_AsksForTheirOwnNumber()
    {
        var handler = Build();

        await SendContactAsync(handler, from: ImpostorTelegramId, SomebodyElsesPhone, contactOwner: CustomerTelegramId);

        Assert.Contains(_messenger.Texts, t => t.Contains("شماره خودتان"));
    }

    /// <summary>
    /// A message with no <c>From</c> at all, carrying a card with no <c>UserId</c>. The handler
    /// reads the sender as <c>message.From?.Id ?? 0</c>, so both sides of the comparison were
    /// absent and the card passed a check meant to reject exactly this. Zero is the seeded root
    /// admin's TelegramId, so the number would have landed on the one account that can charge
    /// credit to anybody.
    /// </summary>
    [Fact]
    public async Task SharingAContactOnAMessageWithNoSender_DoesNotStoreThatNumber()
    {
        var handler = Build();
        _usersApi.UsersByTelegramId[0] = _usersApi.User!;

        await handler.HandleMessageAsync(new Message
        {
            Chat = new Chat { Id = ImpostorTelegramId },
            From = null,
            Contact = new Contact { PhoneNumber = SomebodyElsesPhone, UserId = null }
        });

        Assert.Empty(_usersApi.PhoneUpdates);
    }

    /// <summary>
    /// The button path — the one every real customer uses — still works. This is the half most
    /// likely to be broken by tightening the check.
    /// </summary>
    [Fact]
    public async Task SharingTheirOwnContact_StoresTheNumber()
    {
        var handler = Build();

        await SendContactAsync(handler, from: ImpostorTelegramId, SomebodyElsesPhone, contactOwner: ImpostorTelegramId);

        var stored = Assert.Single(_usersApi.PhoneUpdates);
        Assert.Equal(ImpostorTelegramId, stored.TelegramId);
    }
}
