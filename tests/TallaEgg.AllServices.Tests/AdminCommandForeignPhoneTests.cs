using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// An operator can act on a customer whose number is not Iranian (issue #307).
///
/// <para>
/// Foreign numbers are accepted and stored in full international form — <c>447700900123</c>, 12
/// digits — while every operator command matched <c>\d{10,11}</c>, the length of an Iranian local
/// number. So the decision to accept foreign customers would have stopped at registration: the
/// commands ش, د, ن, م, س, ت and ر could not even parse such a number, and the bot would answer
/// with the admin help text as though the operator had mistyped something.
/// </para>
/// </summary>
public class AdminCommandForeignPhoneTests
{
    private const long AdminTelegramId = 5001;
    private const string ForeignPhone = "447700900123";

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly DepositRecordingWalletApiClient _walletApi = new();

    private sealed class DepositRecordingWalletApiClient : StubWalletApiClient
    {
        public List<WalletRequest> Deposits { get; } = [];

        public override Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request)
        {
            Deposits.Add(request);
            return Task.FromResult(TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Ok(new WalletBallanceDTO
            {
                Asset = request.Asset,
                BalanceBefore = 0,
                BalanceAfter = request.Amount,
                CurrentBalance = request.Amount
            }));
        }
    }

    private readonly UserDto _customer = new()
    {
        Id = Guid.NewGuid(),
        TelegramId = 7002,
        FirstName = "مشتری",
        PhoneNumber = ForeignPhone,
        Status = UserStatus.Approved,
        Role = UserRole.RegularUser
    };

    private BotHandler Build()
    {
        _usersApi.User = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = AdminTelegramId,
            FirstName = "مدیر",
            PhoneNumber = "09209698569",
            Status = UserStatus.Approved,
            Role = UserRole.Admin
        };

        _usersApi.UsersByTelegramId[AdminTelegramId] = _usersApi.User;
        _usersApi.UsersByTelegramId[_customer.TelegramId] = _customer;
        _usersApi.UsersByPhone[ForeignPhone] = _customer;

        return new BotHandler(
            NullLogger<BotHandler>.Instance,
            botClient: null!,
            messenger: _messenger,
            conversations: new InMemoryConversationStore(),
            orderApi: new FakeOrderApiClient(),
            usersApi: _usersApi,
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: _walletApi,
            versionService: new FakeVersionService());
    }

    private Task SayAsync(BotHandler handler, string text) =>
        handler.HandleMessageAsync(new Message
        {
            Text = text,
            Chat = new Chat { Id = AdminTelegramId },
            From = new User { Id = AdminTelegramId }
        });

    /// <summary>Charging credit: the command has to reach the customer it names.</summary>
    [Fact]
    public async Task ChargingCreditOnAForeignNumber_ReachesThatCustomer()
    {
        var handler = Build();

        await SayAsync(handler, $"ش {ForeignPhone} 100");

        var deposit = Assert.Single(_walletApi.Deposits);
        Assert.Equal(_customer.Id, deposit.UserId);
    }

    /// <summary>And the role command, which is the one that hands out operator access.</summary>
    [Fact]
    public async Task ChangingRoleOnAForeignNumber_ReachesThatCustomer()
    {
        var handler = Build();

        await SayAsync(handler, $"ن {ForeignPhone} مدیر");

        var change = Assert.Single(_usersApi.RoleChanges);
        Assert.Equal(_customer.Id, change.UserId);
    }
}
