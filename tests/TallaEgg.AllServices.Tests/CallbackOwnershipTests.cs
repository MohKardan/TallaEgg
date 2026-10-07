using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using Telegram.Bot.Types;
using TallaEgg.AllServices.Tests.Fakes;
using User = Telegram.Bot.Types.User;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Issue #334: four inline-button callbacks carry an account or order id, and none of them checked
/// that the person tapping may see or act on it.
///
/// <para>
/// Callback data is client-side text. A modified Telegram client can send any string back on any
/// message the bot sent it, whether or not that message ever carried such a button — the same reason
/// <c>approve_</c> was gated (<see cref="FirstRunBootstrapTests"/>). So each test here sends the
/// string directly, as an attacker would, rather than tapping a button the bot built.
/// </para>
///
/// <para>
/// The worst of the four was not in the issue as filed: <c>users_{page}_</c> needs no id at all, and
/// listed every customer's name, username and phone number to whoever asked for page 1, 2, 3…
/// </para>
/// </summary>
public class CallbackOwnershipTests
{
    private const long CustomerTelegramId = 262176247;
    private const long OtherCustomerTelegramId = 318540912;
    private const long OperatorTelegramId = 6389449308;
    private const long StrangerTelegramId = 701234567;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OtherCustomerId = Guid.NewGuid();
    private static readonly Guid OperatorId = Guid.NewGuid();

    private readonly FakeBotMessenger _messenger = new();
    private readonly FakeUsersApiClient _usersApi = new();
    private readonly FakeOrderApiClient _orderApi = new()
    {
        UserOrdersPage = new PagedResult<OrderHistoryDto> { PageNumber = 2, PageSize = 5 },
        UserTradesPage = new PagedResult<TradeHistoryDto> { PageNumber = 2, PageSize = 5 },
        ActiveOrdersByUser = [],
        AllowCancel = true
    };

    public CallbackOwnershipTests()
    {
        _usersApi.UsersByTelegramId[CustomerTelegramId] =
            Person(CustomerId, CustomerTelegramId, "09158527483", UserRole.RegularUser);
        _usersApi.UsersByTelegramId[OtherCustomerTelegramId] =
            Person(OtherCustomerId, OtherCustomerTelegramId, "09121234567", UserRole.RegularUser);
        _usersApi.UsersByTelegramId[OperatorTelegramId] =
            Person(OperatorId, OperatorTelegramId, "09209698569", UserRole.Admin);
        _usersApi.UsersPage = new PagedResult<UserDto> { PageNumber = 1, PageSize = 5 };
    }

    private static UserDto Person(Guid id, long telegramId, string phone, UserRole role) => new()
    {
        Id = id,
        TelegramId = telegramId,
        FirstName = "کاربر",
        PhoneNumber = phone,
        Status = UserStatus.Approved,
        Role = role
    };

    private BotHandler Build() => new(
        NullLogger<BotHandler>.Instance,
        botClient: null!,
        messenger: _messenger,
        conversations: new InMemoryConversationStore(),
        orderApi: _orderApi,
        usersApi: _usersApi,
        affiliateApi: new FakeAffiliateApiClient(),
        walletApi: new StubWalletApiClient(),
        versionService: new FakeVersionService());

    private Task SendCallbackAsync(string callbackData, long from) =>
        Build().HandleCallbackQueryAsync(new CallbackQuery
        {
            Id = "cb-334",
            Data = callbackData,
            From = new User { Id = from },
            Message = new Message { Id = 1, Text = "…", Chat = new Chat { Id = from } }
        });

    // ── users_: the customer list ───────────────────────────────────────────────

    [Fact]
    public async Task UsersPage_SentByACustomer_DoesNotListAnyone()
    {
        await SendCallbackAsync("users_1_", from: CustomerTelegramId);

        Assert.Empty(_usersApi.UsersPagesRequested);
        Assert.Empty(_messenger.Edited);
    }

    [Fact]
    public async Task UsersPage_SentBySomeoneWithNoAccount_DoesNotListAnyone()
    {
        await SendCallbackAsync("users_2_", from: StrangerTelegramId);

        Assert.Empty(_usersApi.UsersPagesRequested);
    }

    [Fact]
    public async Task UsersPage_SentByAnOperator_StillPages()
    {
        await SendCallbackAsync("users_2_", from: OperatorTelegramId);

        Assert.Equal([2], _usersApi.UsersPagesRequested);
    }

    // ── trades_: one account's trade history ────────────────────────────────────

    [Fact]
    public async Task TradesPage_ForAnotherCustomer_IsRefused()
    {
        await SendCallbackAsync($"trades_{OtherCustomerId}_2", from: CustomerTelegramId);

        Assert.Empty(_orderApi.UserTradesRequested);
        Assert.Empty(_messenger.Edited);
    }

    [Fact]
    public async Task TradesPage_ForTheirOwnAccount_StillPages()
    {
        await SendCallbackAsync($"trades_{CustomerId}_2", from: CustomerTelegramId);

        Assert.Equal([CustomerId], _orderApi.UserTradesRequested);
    }

    /// <summary>
    /// «معامله &lt;phone&gt;» shows an operator a customer's trades with paging buttons carrying
    /// that customer's id, so for an operator the id is legitimately someone else's.
    /// </summary>
    [Fact]
    public async Task TradesPage_ForACustomer_SentByAnOperator_StillPages()
    {
        await SendCallbackAsync($"trades_{CustomerId}_2", from: OperatorTelegramId);

        Assert.Equal([CustomerId], _orderApi.UserTradesRequested);
    }

    // ── orders_: one account's order history ────────────────────────────────────

    [Fact]
    public async Task OrdersPage_ForAnotherCustomer_IsRefused()
    {
        await SendCallbackAsync($"orders_{OtherCustomerId}_2", from: CustomerTelegramId);

        Assert.Empty(_orderApi.UserOrdersRequested);
        Assert.Empty(_messenger.Edited);
    }

    [Fact]
    public async Task OrdersPage_ForTheirOwnAccount_StillPages()
    {
        await SendCallbackAsync($"orders_{CustomerId}_2", from: CustomerTelegramId);

        Assert.Equal([CustomerId], _orderApi.UserOrdersRequested);
    }

    // ── cancel_order_ ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrder_BelongingToAnotherCustomer_IsNotCancelled()
    {
        var victimsOrder = Guid.NewGuid();
        _orderApi.ActiveOrdersByUser![OtherCustomerId] = [new OrderHistoryDto { Id = victimsOrder }];

        await SendCallbackAsync($"cancel_order_{victimsOrder}", from: CustomerTelegramId);

        Assert.Empty(_orderApi.CancelledOrders);
    }

    [Fact]
    public async Task CancelOrder_TheirOwnActiveOrder_IsCancelled()
    {
        var ownOrder = Guid.NewGuid();
        _orderApi.ActiveOrdersByUser![CustomerId] = [new OrderHistoryDto { Id = ownOrder }];

        await SendCallbackAsync($"cancel_order_{ownOrder}", from: CustomerTelegramId);

        Assert.Equal([ownOrder], _orderApi.CancelledOrders);
    }

    [Fact]
    public async Task CancelOrder_SentByAnOperator_IsCancelledWhoeverOwnsIt()
    {
        var customersOrder = Guid.NewGuid();
        _orderApi.ActiveOrdersByUser![CustomerId] = [new OrderHistoryDto { Id = customersOrder }];

        await SendCallbackAsync($"cancel_order_{customersOrder}", from: OperatorTelegramId);

        Assert.Equal([customersOrder], _orderApi.CancelledOrders);
    }
}
