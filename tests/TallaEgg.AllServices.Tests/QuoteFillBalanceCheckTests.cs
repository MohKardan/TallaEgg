using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A customer cannot trade on a quote without the funds or credit to cover it.
///
/// <para>
/// <b>This was broken, and it let money be created from nothing.</b> In the order-book model
/// the check lived in <c>OrderService.SubmitOrderAsync</c>. The dealer model (issue #48) does
/// not go through that method — <c>QuoteFillService</c> creates both orders itself — so the
/// check was dropped without anything failing. The only thing left standing between a customer
/// and a trade was the collateral lock, and <c>Wallet.LockBalance</c> performs no check at all:
/// its guard is commented out so the market maker's account can go negative, which is correct
/// for the market maker and catastrophic for everybody else.
/// </para>
///
/// <para>
/// Observed on an empty database: a customer registered, was approved, held nothing, bought one
/// gram of gold, and finished with a balance of −18,237,222 tomans. Another sold 23 grams they
/// did not own. Nothing errored and nothing was logged above Information — the trades settled
/// cleanly, which is what made it invisible until the balances were read directly.
/// </para>
///
/// <para>
/// It only appeared now because every account on the previous database already had credit. That
/// is the general lesson: an empty database exercises paths a populated one never reaches.
/// </para>
/// </summary>
public class QuoteFillBalanceCheckTests : IDisposable
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private const decimal BuyPrice = 17_000_000m;
    private const decimal SellPrice = 17_200_000m;

    private readonly SqliteConnection _connection;
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _marketMakerId = Guid.NewGuid();

    public QuoteFillBalanceCheckTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    /// <summary>
    /// Answers the balance question with whatever the test dictates, and records every lock so
    /// a test can prove that a refused fill never reached the wallet at all.
    /// </summary>
    private sealed class BalanceStub : StubWalletApiClient
    {
        public bool CheckSucceeds { get; set; } = true;
        public bool HasBaseAsset { get; set; }
        public bool HasQuoteAsset { get; set; }

        public List<(Guid UserId, string Asset, decimal Amount)> Locks { get; } = [];

        /// <summary>Whose balance was asked about, in order.</summary>
        public List<Guid> Asked { get; } = [];

        public override Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
            ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price)
        {
            Asked.Add(userId);
            return Task.FromResult((CheckSucceeds, "checked", HasBaseAsset, HasQuoteAsset));
        }

        public override Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
            Guid userId, string asset, decimal amount)
        {
            Locks.Add((userId, asset, amount));
            return Task.FromResult((true, "locked", (WalletDTO?)new WalletDTO()));
        }

        public override Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount) =>
            Task.FromResult((true, "unlocked"));
    }

    private readonly BalanceStub _wallet = new();

    private QuoteFillService BuildService()
    {
        var context = NewContext();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:MarketModes:" + Symbol] = "Dealer",
                ["Matching:MarketMakerUserId"] = _marketMakerId.ToString()
            })
            .Build();

        var marketMode = new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);
        var orderRepository = new OrderRepository(context, NullLogger<OrderRepository>.Instance);
        var tradeRepository = new TradeRepository(context);
        var matchingRepository = new OrderMatchingRepository(
            context, NullLogger<OrderMatchingRepository>.Instance);

        var orderService = new Orders.Application.OrderService(
            orderRepository,
            _wallet,
            new NoOpMatchingEngine(),
            NullLogger<Orders.Application.OrderService>.Instance,
            UsersApiClient: null!,
            new OrderCollateralReconciler(orderRepository, tradeRepository, _wallet,
                NullLogger<OrderCollateralReconciler>.Instance),
            quoteRepository,
            marketMode);

        return new QuoteFillService(
            quoteRepository, orderService, marketMode, matchingRepository, _wallet,
            NullLogger<QuoteFillService>.Instance);
    }

    /// <summary>Publishes the quote the customer will trade against.</summary>
    private async Task PublishQuoteAsync()
    {
        await using var db = NewContext();
        db.Quotes.Add(Quote.Publish(Symbol, BuyPrice, SellPrice, _marketMakerId));
        await db.SaveChangesAsync();
    }

    private async Task<int> OrderCountAsync()
    {
        await using var db = NewContext();
        return await db.Orders.CountAsync();
    }

    // ── the customer must be covered ────────────────────────────────────────────

    /// <summary>
    /// The exact case seen in production: a customer with nothing buys gold. Before the fix
    /// this produced a settled trade and a negative balance.
    /// </summary>
    [Fact]
    public async Task ACustomerWithNoFundsCannotBuy()
    {
        await PublishQuoteAsync();
        _wallet.HasQuoteAsset = false;
        _wallet.HasBaseAsset = false;

        var (success, message, trade) =
            await BuildService().AcceptQuoteAsync(_customerId, Symbol, OrderSide.Buy, 1m);

        Assert.False(success);
        Assert.Null(trade);
        Assert.Contains("کافی نیست", message);
    }

    /// <summary>And the mirror image: selling gold they do not hold.</summary>
    [Fact]
    public async Task ACustomerWithNoGoldCannotSell()
    {
        await PublishQuoteAsync();
        _wallet.HasBaseAsset = false;
        _wallet.HasQuoteAsset = true;   // plenty of money, none of the asset being sold

        var (success, _, trade) =
            await BuildService().AcceptQuoteAsync(_customerId, Symbol, OrderSide.Sell, 23m);

        Assert.False(success);
        Assert.Null(trade);
    }

    /// <summary>
    /// Nothing at all is created when the customer is refused — no order, and no lock on either
    /// side. Refusing after creating an order would leave rows to clean up and, for the market
    /// maker's side, collateral locked against a trade that never happens.
    /// </summary>
    [Fact]
    public async Task ARefusedFillCreatesNoOrderAndLocksNothing()
    {
        await PublishQuoteAsync();
        _wallet.HasQuoteAsset = false;

        await BuildService().AcceptQuoteAsync(_customerId, Symbol, OrderSide.Buy, 1m);

        Assert.Equal(0, await OrderCountAsync());
        Assert.Empty(_wallet.Locks);
    }

    /// <summary>
    /// The side being checked follows the side being traded. A buy is paid for in the quote
    /// asset and a sell in the base asset; checking the wrong one would let a customer buy gold
    /// on the strength of gold they hold rather than money.
    /// </summary>
    [Fact]
    public async Task HoldingOnlyTheAssetBeingBoughtDoesNotPayForBuyingMoreOfIt()
    {
        await PublishQuoteAsync();
        _wallet.HasBaseAsset = true;     // holds gold
        _wallet.HasQuoteAsset = false;   // holds no money

        var (success, _, _) = await BuildService().AcceptQuoteAsync(_customerId, Symbol, OrderSide.Buy, 1m);

        Assert.False(success);
    }

    /// <summary>
    /// When the wallet service cannot answer, the fill is refused rather than allowed. An
    /// unreachable check is not a passed check — and this is the direction that matters, because
    /// the failure mode of the opposite choice is silent and expensive.
    /// </summary>
    [Fact]
    public async Task AnUnreachableWalletServiceRefusesTheFill()
    {
        await PublishQuoteAsync();
        _wallet.CheckSucceeds = false;
        _wallet.HasQuoteAsset = true;

        var (success, _, _) = await BuildService().AcceptQuoteAsync(_customerId, Symbol, OrderSide.Buy, 1m);

        Assert.False(success);
        Assert.Equal(0, await OrderCountAsync());
    }

    // ── and a funded customer must still be able to trade ───────────────────────

    /// <summary>
    /// The other direction, without which the fix could simply be "refuse everything". A covered
    /// customer reaches the point of having collateral locked for both sides.
    ///
    /// <para>
    /// The match itself is not asserted here: <c>ExecuteAtomicMatchAsync</c> orders by a decimal
    /// column, which SQLite cannot do. What this pins is that the balance check let the flow
    /// through — the thing the guard decides.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ACoveredCustomerIsNotBlocked()
    {
        await PublishQuoteAsync();
        _wallet.HasQuoteAsset = true;
        _wallet.HasBaseAsset = true;

        await BuildService().AcceptQuoteAsync(_customerId, Symbol, OrderSide.Buy, 1m);

        Assert.Contains(_wallet.Locks, l => l.UserId == _customerId);
        Assert.Contains(_wallet.Locks, l => l.UserId == _marketMakerId);
    }

    /// <summary>
    /// The market maker is deliberately exempt. In the dealer model the shop is the counterparty
    /// to every trade and its position is the business's book, not an error — so the check is
    /// asked about the customer only. If it were asked about the market maker too, the shop
    /// would be unable to sell gold it has not yet bought, which is the whole business.
    /// </summary>
    [Fact]
    public async Task TheBalanceCheckIsAskedAboutTheCustomerAndNotTheMarketMaker()
    {
        await PublishQuoteAsync();
        _wallet.HasQuoteAsset = true;
        _wallet.HasBaseAsset = true;

        await BuildService().AcceptQuoteAsync(_customerId, Symbol, OrderSide.Buy, 1m);

        Assert.Equal([_customerId], _wallet.Asked);
    }

    /// <summary>The quote path never matches through the engine; it matches once, itself.</summary>
    private sealed class NoOpMatchingEngine : Orders.Application.IMatchingEngine
    {
        public Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
