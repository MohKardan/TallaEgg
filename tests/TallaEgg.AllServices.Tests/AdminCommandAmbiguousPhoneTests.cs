using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What the operator commands do when a phone number resolves to more than one account
/// (issue #303): nothing at all, and they say why.
///
/// <para>
/// Every command that takes a number — ش, د, م, س, ن, ت, ر — used to resolve it through a
/// lookup that returned the first matching row. Ids are <c>Guid.NewGuid()</c>, so "first" is
/// not something the code chooses: charging a customer's credit could land on an account that
/// had merely claimed their number, and promoting a customer could promote that account
/// instead. The refusal now comes from the Users service, which is the only layer that can
/// tell "nobody holds this number" from "two accounts do".
/// </para>
/// </summary>
public class AdminCommandAmbiguousPhoneTests
{
    private const long AdminTelegramId = 5001;
    private const long CustomerTelegramId = 7002;
    private const string AmbiguousPhone = "09158527483";
    private const string Refusal = "این شماره روی بیش از یک حساب ثبت شده است و تا اصلاح آن، دستور روی هیچ حسابی اجرا نمی‌شود.";

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly DepositRecordingWalletApiClient _walletApi = new();

    /// <summary>Records what the wallet was asked to do, so "nothing happened" is checkable.</summary>
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

        // The number the operator types answers with a refusal rather than a customer, which is
        // what the Users API does once two accounts hold it.
        _usersApi.PhoneLookupRefusals[AmbiguousPhone] = Refusal;

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

    [Fact]
    public async Task ChargingCreditOnAnAmbiguousNumber_MovesNoMoney()
    {
        var handler = Build();

        await SayAsync(handler, $"ش {AmbiguousPhone} 100");

        Assert.Empty(_walletApi.Deposits);
    }

    /// <summary>
    /// The operator has to learn which of the two situations this is: a mistyped number is
    /// theirs to fix, a number on two accounts is not.
    /// </summary>
    [Fact]
    public async Task ChargingCreditOnAnAmbiguousNumber_ShowsTheServicesRefusal()
    {
        var handler = Build();

        await SayAsync(handler, $"ش {AmbiguousPhone} 100");

        Assert.Contains(_messenger.Texts, t => t.Contains("بیش از یک حساب"));
    }

    /// <summary>
    /// The one that turns a stranger into an operator. An impostor holding a customer's number
    /// would have had a coin's chance of being handed the Admin role meant for the customer.
    /// </summary>
    [Fact]
    public async Task ChangingRoleOnAnAmbiguousNumber_ChangesNoRole()
    {
        var handler = Build();

        await SayAsync(handler, $"ن {AmbiguousPhone} مدیر");

        Assert.Empty(_usersApi.RoleChanges);
    }

    [Fact]
    public async Task ApprovingAnAmbiguousNumber_ApprovesNobody()
    {
        var handler = Build();

        await SayAsync(handler, $"ت {AmbiguousPhone}");

        Assert.Empty(_usersApi.StatusChanges);
    }

    /// <summary>
    /// The other half of the same guard: a number that resolves to exactly one customer still
    /// works. Without this, refusing everything would pass the tests above.
    /// </summary>
    [Fact]
    public async Task ChargingCreditOnAnUnambiguousNumber_StillMovesMoney()
    {
        var handler = Build();
        const string CustomerPhone = "09120000000";
        _usersApi.UsersByPhone[CustomerPhone] = new UserDto
        {
            Id = Guid.NewGuid(),
            TelegramId = CustomerTelegramId,
            FirstName = "مشتری",
            PhoneNumber = CustomerPhone,
            Status = UserStatus.Approved,
            Role = UserRole.RegularUser
        };
        _usersApi.UsersByPhone[_usersApi.User!.PhoneNumber!] = _usersApi.User;

        await SayAsync(handler, $"ش {CustomerPhone} 100");

        Assert.Equal(100m, Assert.Single(_walletApi.Deposits).Amount);
    }
}
