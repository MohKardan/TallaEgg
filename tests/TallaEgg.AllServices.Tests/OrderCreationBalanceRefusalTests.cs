using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Responses.Order;
using TallaEgg.Core.ErrorHandling;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// An under-funded order is refused with a reason, and with nothing else (issue #284).
///
/// <para>
/// <b>What was wrong:</b> <c>CreateOrderAsync</c> built the refusal as
/// «موجودی ناکافی: {balanceMessage}». On this branch the balance check itself <i>succeeded</i> —
/// only the sufficiency flag is false — so <c>balanceMessage</c> is the wallet client's success
/// text, and the customer read «موجودی ناکافی: اعتبار و موجودی کاربر بررسی شد»: a log status line
/// pasted onto the end of a reason.
/// </para>
///
/// <para>
/// The two public paths refuse for the same cause, so they now say the same sentence. These tests
/// drive both and assert they agree, and that the wallet's internal status text reaches neither.
/// </para>
/// </summary>
public class OrderCreationBalanceRefusalTests : IDisposable
{
    private const string Gold = CurrenciesConstant.MAUA_IRT;

    // The gold price on 2026-09-10, per gram.
    private const decimal GoldBuyPrice = 17_000_000m;
    private const decimal GoldSellPrice = 17_200_000m;

    /// <summary>
    /// What <see cref="WalletApiClient.ValidateCreditAndBalanceAsync"/> reports when the check ran
    /// to completion. It says the check happened, not that the funds are there, and it is written
    /// for a log line — which is the whole of issue #284.
    /// </summary>
    private const string WalletStatusLine = "اعتبار و موجودی کاربر بررسی شد";

    private readonly SqliteConnection _connection;
    private readonly Guid _customer = Guid.NewGuid();
    private readonly Guid _dealer = Guid.NewGuid();
    private readonly UnderfundedWallet _wallet = new();

    public OrderCreationBalanceRefusalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// The defect itself: the wallet's status line must not be part of what the caller is told.
    /// </summary>
    [Fact]
    public async Task CreateOrderAsync_InsufficientFunds_DoesNotAppendTheWalletsStatusLine()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync());

        Assert.DoesNotContain(WalletStatusLine, ex.Message);
    }

    /// <summary>
    /// Both public paths refuse for the same cause, so a customer must not be able to tell which
    /// one they hit from the wording.
    /// </summary>
    [Fact]
    public async Task CreateOrderAsync_InsufficientFunds_SaysWhatTheQuoteFillPathSays()
    {
        await PublishQuoteAsync();

        var (success, quoteFillMessage, _) =
            await BuildQuoteFillService().AcceptQuoteAsync(_customer, Gold, OrderSide.Buy, 1m);
        Assert.False(success, "Precondition: the quote-fill path must refuse this order too.");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync());

        Assert.Equal(quoteFillMessage, ex.Message);
    }

    /// <summary>
    /// The refusal still has to name its cause. Guards against fixing the extra text by dropping
    /// the reason with it.
    /// </summary>
    [Fact]
    public async Task CreateOrderAsync_InsufficientFunds_StillStatesTheReason()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync());

        Assert.Contains("موجودی", ex.Message);
        Assert.Contains("کافی نیست", ex.Message);
    }

    /// <summary>
    /// The sibling branch a few lines above is not part of this change: when the check itself
    /// fails, the wallet's message is the failure and belongs in what the caller sees.
    /// </summary>
    [Fact]
    public async Task CreateOrderAsync_BalanceCheckFails_StillCarriesTheWalletsFailureMessage()
    {
        _wallet.CheckSucceeds = false;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync());

        Assert.Contains(UnderfundedWallet.CheckFailureMessage, ex.Message);
    }

    /// <summary>
    /// A refusal must leave nothing behind — no resting order, no collateral held against a trade
    /// that never happened.
    /// </summary>
    [Fact]
    public async Task CreateOrderAsync_InsufficientFunds_CreatesNoOrderAndLocksNothing()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync());

        await using var db = NewContext();
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(_wallet.Locks);
    }

    // ── harness ─────────────────────────────────────────────────────────────────

    private Task<CreateOrderResponse> CreateOrderAsync() =>
        BuildOrderService().CreateOrderAsync(new OrderDto
        {
            Asset = Gold,
            Amount = 1m,
            Price = GoldSellPrice,
            UserId = _customer,
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            TradingType = TradingType.Spot
        });

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    private async Task PublishQuoteAsync()
    {
        await using var db = NewContext();
        db.Quotes.Add(Quote.Publish(Gold, GoldBuyPrice, GoldSellPrice, _dealer));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Reports a completed check that found too little: <c>Success</c> true, both sufficiency
    /// flags false — the exact combination issue #284 is about.
    /// </summary>
    private sealed class UnderfundedWallet : StubWalletApiClient
    {
        public const string CheckFailureMessage = "خطا در بررسی موجودی: خطا در ارتباط با سرویس کیف پول";

        public List<(Guid UserId, string Asset, decimal Amount)> Locks { get; } = new();

        /// <summary>False drives the sibling branch, where the check itself did not run.</summary>
        public bool CheckSucceeds { get; set; } = true;

        public override Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
            ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price) =>
            Task.FromResult(CheckSucceeds
                ? (true, WalletStatusLine, false, false)
                : (false, CheckFailureMessage, false, false));

        public override Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
            Guid userId, string asset, decimal amount)
        {
            Locks.Add((userId, asset, amount));
            return Task.FromResult((true, "locked", (WalletDTO?)new WalletDTO()));
        }

        public override Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount) =>
            Task.FromResult((true, "unlocked"));
    }

    private IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Matching:MarketModes:" + Gold] = "Dealer",
            // Required by the UsersApiClient constructor (issue #205); the canned handler answers
            // whatever is asked, so the host is only a label.
            ["UsersApiUrl"] = "http://users.test/api"
        })
        .Build();

    private OrderService BuildOrderService(OrdersDbContext? context = null)
    {
        context ??= NewContext();
        var configuration = Configuration();

        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);

        // 404 rather than a user: the caller is not an administrator, so the customer's path runs.
        var usersApiClient = new UsersApiClient(
            new HttpClient(new NotFoundHandler()), configuration, NullLogger<UsersApiClient>.Instance);

        return new OrderService(
            orderRepository, _wallet, new NoOpMatchingEngine(),
            NullLogger<OrderService>.Instance,
            usersApiClient,
            new OrderCollateralReconciler(orderRepository, new TradeRepository(context), _wallet,
                NullLogger<OrderCollateralReconciler>.Instance),
            new QuoteRepository(context, NullLogger<QuoteRepository>.Instance),
            new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance));
    }

    private QuoteFillService BuildQuoteFillService()
    {
        var context = NewContext();
        var configuration = Configuration();

        return new QuoteFillService(
            new QuoteRepository(context, NullLogger<QuoteRepository>.Instance),
            BuildOrderService(context),
            new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance),
            new OrderMatchingRepository(context, NullLogger<OrderMatchingRepository>.Instance),
            _wallet, NullLogger<QuoteFillService>.Instance);
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"success\":false}", Encoding.UTF8, "application/json")
            });
    }

    private sealed class NoOpMatchingEngine : IMatchingEngine
    {
        public Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
