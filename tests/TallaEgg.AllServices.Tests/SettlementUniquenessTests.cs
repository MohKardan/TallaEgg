using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// "Every trade settles exactly once" must be guaranteed by the database, not by the order in which
/// code happens to run.
///
/// The only protection used to be a SELECT running 46 lines before the transaction opened, with no
/// uniqueness constraint behind it. Two concurrent settlements of one trade could both pass that
/// check and both apply — crediting each side twice and creating money from
/// می‌شد (issue #42).
///
/// TradeId is now the primary key of TradeSettlements, and that row is inserted inside the same
/// transaction that moves the money.
/// </summary>
public class SettlementUniquenessTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly HookedWalletDbContext _context;
    private readonly WalletRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Base = "MAUA";
    private const string Quote = "IRT";
    private const decimal Quantity = 2m;
    private const decimal QuoteQuantity = 1000m;

    /// <summary>
    /// A hook just before SaveChanges, to reproduce deterministically and without a real race the
    /// case where a competitor commits in the window between our fast check and our insert.
    ///
    /// Why this rather than genuinely running two parallel tasks: in-memory SQLite on a single
    /// connection does not support two concurrent write transactions, so a parallel test would
    /// either deadlock or become timing-dependent and flaky. What needs proving is not that a race
    /// can happen, but that when it does the database stops it and the code translates that
    /// correctly — which is exactly what this exercises, against the real constraint.
    /// </summary>
    private sealed class HookedWalletDbContext : WalletDbContext
    {
        public Action? BeforeSave;

        public HookedWalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var hook = BeforeSave;
            BeforeSave = null; // فقط یک بار اجرا شود، وگرنه rollback هم دوباره آن را صدا می‌زند
            hook?.Invoke();
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    public SettlementUniquenessTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;
        _context = new HookedWalletDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);

        SeedFullyLockedWallets();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedWallet(Guid userId, string asset, decimal balance, decimal locked)
    {
        var wallet = WalletEntity.Create(userId, asset);
        wallet.Balance = balance;
        wallet.LockedBalance = locked;
        _context.Wallets.Add(wallet);
    }

    private void SeedFullyLockedWallets()
    {
        SeedWallet(_buyerId, Quote, balance: 0m, locked: QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();
    }

    private Task<(bool Success, string Message)> SettleAsync(Guid tradeId) =>
        _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);

    private async Task<WalletEntity> ReloadAsync(Guid userId, string asset)
    {
        _context.ChangeTracker.Clear();
        return await _context.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == asset);
    }

    /// <summary>An ordinary settlement must record its settlement row — the basis of every guarantee below.</summary>
    [Fact]
    public async Task Settlement_RecordsExactlyOneTradeSettlementRow()
    {
        var tradeId = Guid.NewGuid();

        var (success, _) = await SettleAsync(tradeId);

        Assert.True(success);
        Assert.Equal(1, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
    }

    /// <summary>
    /// The core case from #42: a competitor commits after we have passed the fast check. Our insert
    /// must fail on the primary key and the whole transaction must roll back.
    /// </summary>
    [Fact]
    public async Task ConcurrentSettlement_IsRefusedByTheDatabase_AndReportedAsAlreadySettled()
    {
        var tradeId = Guid.NewGuid();

        // The competitor inserts the settlement row directly, just before our SaveChanges — the
        // moment at which our fast check has already passed and can no longer help.
        _context.BeforeSave = () =>
            _context.Database.ExecuteSqlRaw(
                @"INSERT INTO TradeSettlements
                      (TradeId, SettledAt, Symbol, Quantity, QuoteQuantity, BuyerUserId, SellerUserId)
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                tradeId, DateTime.UtcNow, $"{Base}/{Quote}", Quantity, QuoteQuantity, _buyerId, _sellerId);

        var (success, message) = await SettleAsync(tradeId);

        // From the caller's point of view this succeeded: the trade is settled, just not by us.
        // Returning an error would make the outbox processor retry five times and raise a healthy
        // trade to an operator as stuck.
        Assert.True(success, message);
        Assert.Contains("already settled", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The claim that matters most: the loser of the race must have moved no money at all. That is
    /// what #42 was about — not an error message, but a doubled balance.
    /// </summary>
    [Fact]
    public async Task ConcurrentSettlement_LeavesBalancesAndTransactionsUntouched()
    {
        var tradeId = Guid.NewGuid();

        _context.BeforeSave = () =>
            _context.Database.ExecuteSqlRaw(
                @"INSERT INTO TradeSettlements
                      (TradeId, SettledAt, Symbol, Quantity, QuoteQuantity, BuyerUserId, SellerUserId)
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                tradeId, DateTime.UtcNow, $"{Base}/{Quote}", Quantity, QuoteQuantity, _buyerId, _sellerId);

        await SettleAsync(tradeId);

        // No transaction row was written at all — not written and then compensated.
        Assert.Equal(0, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));

        // And the balances are exactly as they were before: collateral still locked, nothing moved.
        var buyerQuote = await ReloadAsync(_buyerId, Quote);
        var buyerBase = await ReloadAsync(_buyerId, Base);
        var sellerBase = await ReloadAsync(_sellerId, Base);
        var sellerQuote = await ReloadAsync(_sellerId, Quote);

        Assert.Equal(QuoteQuantity, buyerQuote.LockedBalance);
        Assert.Equal(0m, buyerQuote.Balance);
        Assert.Equal(0m, buyerBase.Balance);
        Assert.Equal(Quantity, sellerBase.LockedBalance);
        Assert.Equal(0m, sellerBase.Balance);
        Assert.Equal(0m, sellerQuote.Balance);
    }

    /// <summary>
    /// An ordinary outbox redelivery, with no race involved, must take the fast path: no exception,
    /// no transaction, and no second row.
    /// </summary>
    [Fact]
    public async Task RedeliveryAfterSuccess_IsIdempotent_AndDoesNotDoubleApply()
    {
        var tradeId = Guid.NewGuid();

        var first = await SettleAsync(tradeId);
        var second = await SettleAsync(tradeId);

        Assert.True(first.Success);
        Assert.True(second.Success);

        Assert.Equal(1, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));

        // The balances moved exactly once.
        Assert.Equal(Quantity, (await ReloadAsync(_buyerId, Base)).Balance);
        Assert.Equal(QuoteQuantity, (await ReloadAsync(_sellerId, Quote)).Balance);
    }

    /// <summary>
    /// Two different trades must not block each other — the uniqueness constraint has to be on the
    /// trade id and not on something trades happen to share, such as the parties or the symbol.
    /// </summary>
    [Fact]
    public async Task DifferentTrades_BetweenTheSameParties_BothSettle()
    {
        var firstTrade = Guid.NewGuid();
        var secondTrade = Guid.NewGuid();

        // Lock the collateral the second trade needs as well.
        //
        // Both wallets are loaded in one unit of work and the ChangeTracker is not cleared between
        // them; otherwise the first entity would detach and its change would never be saved.
        _context.ChangeTracker.Clear();
        var buyerQuote = await _context.Wallets.SingleAsync(w => w.UserId == _buyerId && w.Asset == Quote);
        var sellerBase = await _context.Wallets.SingleAsync(w => w.UserId == _sellerId && w.Asset == Base);
        buyerQuote.LockedBalance += QuoteQuantity;
        sellerBase.LockedBalance += Quantity;
        await _context.SaveChangesAsync();

        var a = await SettleAsync(firstTrade);
        var b = await SettleAsync(secondTrade);

        Assert.True(a.Success, a.Message);
        Assert.True(b.Success, b.Message);
        Assert.Equal(2, await _context.TradeSettlements.CountAsync());
        Assert.Equal(8, await _context.Transactions.CountAsync());
    }
}
