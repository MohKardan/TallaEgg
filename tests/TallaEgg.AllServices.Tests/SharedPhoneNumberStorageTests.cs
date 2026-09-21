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
/// The phone number a customer shares is stored as their number, not a corrupted one (issue #297).
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong:</b> Telegram usually sends a shared contact's number with the country code and
/// no plus, <c>989151198161</c>. The handler checked for the <c>98</c> prefix and then called
/// <c>Replace("98", "0")</c>, which replaces every occurrence, so that number was stored as
/// <c>0915110161</c> — ten digits, and not the customer's. Every admin command looks a customer up
/// by phone, so a customer stored that way could not be approved, credited or found. The defect
/// dates from 2025-08-21.
/// </para>
/// <para>
/// It went unseen because the simulator sends numbers as <c>+98…</c>, and the <c>+98</c> branch
/// was never wrong. These tests drive the handler with the shape Telegram actually sends.
/// </para>
/// </remarks>
public class SharedPhoneNumberStorageTests
{
    private const long CustomerTelegramId = 262176247;

    private readonly FakeUsersApiClient _usersApi = new()
    {
        User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = CustomerTelegramId,
            FirstName = "مشتری",
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser
        }
    };

    private BotHandler Build() => new(
        NullLogger<BotHandler>.Instance,
        botClient: null!,
        messenger: new FakeBotMessenger(),
        conversations: new InMemoryConversationStore(),
        orderApi: new FakeOrderApiClient(),
        usersApi: _usersApi,
        affiliateApi: new FakeAffiliateApiClient(),
        walletApi: new StubWalletApiClient(),
        versionService: new FakeVersionService());

    private async Task<string> StoredAfterSharingAsync(string sharedByTelegram)
    {
        await Build().HandleMessageAsync(new Message
        {
            Chat = new Chat { Id = CustomerTelegramId },
            From = new User { Id = CustomerTelegramId },
            Contact = new Contact { PhoneNumber = sharedByTelegram, UserId = CustomerTelegramId }
        });

        return Assert.Single(_usersApi.PhoneUpdates).PhoneNumber;
    }

    /// <summary>The defect: <c>98</c> inside the number must survive.</summary>
    [Theory]
    [InlineData("989151198161", "09151198161")]
    [InlineData("989812345678", "09812345678")]
    [InlineData("989129898989", "09129898989")]
    public async Task ANumberWith98AfterTheCountryCode_IsStoredIntact(string shared, string expected)
    {
        Assert.Equal(expected, await StoredAfterSharingAsync(shared));
    }

    /// <summary>The shapes that were already right stay right.</summary>
    [Theory]
    [InlineData("989151234567", "09151234567")]
    [InlineData("+989151234567", "09151234567")]
    [InlineData("+989151198161", "09151198161")]
    [InlineData("09151234567", "09151234567")]
    public async Task TheUsualShapes_AreStoredAsALocalNumber(string shared, string expected)
    {
        Assert.Equal(expected, await StoredAfterSharingAsync(shared));
    }

    /// <summary>
    /// A number from outside Iran is accepted — the owner settled that on 2026-09-21, and one
    /// real account on this database already has one — and stored as its digits with the country
    /// code. The plus goes, because the country code already says what it said, and because a
    /// number stored two ways is two accounts as far as the duplicate rule is concerned
    /// (issue #307). This test previously asserted the number came through untouched, back when
    /// #297 deliberately left the question open.
    /// </summary>
    [Fact]
    public async Task ANonIranianNumber_IsAcceptedInInternationalForm()
    {
        Assert.Equal("447700900123", await StoredAfterSharingAsync("+447700900123"));
    }
}
