using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.Enums.Wallet;
using Wallet.Core;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// Integration tests for the money path: WalletRepository.SettleTradeAsync.
/// Uses a real SQLite in-memory database (supports transactions, unlike EF InMemory)
/// so the atomic settlement and its DB transaction are actually exercised.
///
/// Scenario throughout: trading pair MAUA/IRT, price 500, quantity 2 => quoteQuantity 1000.
/// Buyer locked 1000 IRT as collateral; seller locked 2 MAUA as collateral.
/// </summary>
public class SettleTradeAsyncTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WalletDbContext _context;
    private readonly WalletRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Base = "MAUA";  // gold
    private const string Quote = "IRT";  // toman
    private const decimal Quantity = 2m;
    private const decimal QuoteQuantity = 1000m; // price 500 * quantity 2

    public SettleTradeAsyncTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new WalletDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private WalletEntity SeedWallet(Guid userId, string asset, decimal balance, decimal locked)
    {
        var wallet = WalletEntity.Create(userId, asset);
        wallet.Balance = balance;
        wallet.LockedBalance = locked;
        _context.Wallets.Add(wallet);
        return wallet;
    }

    /// <summary>Seed the four wallets with collateral already locked (the normal pre-settlement state).</summary>
    private void SeedFullyLockedWallets()
    {
        SeedWallet(_buyerId, Quote, balance: 0m, locked: QuoteQuantity); // buyer's IRT locked
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);             // buyer's MAUA (to receive)
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);      // seller's MAUA locked
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);           // seller's IRT (to receive)
        _context.SaveChanges();
    }

    private async Task<WalletEntity> ReloadAsync(Guid userId, string asset)
    {
        _context.ChangeTracker.Clear(); // force a read from the database, not tracked memory
        return await _context.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == asset);
    }

    [Fact]
    public async Task SettleTradeAsync_HappyPath_MovesBalancesConsumesLockAndRecordsTransactions()
    {
        SeedFullyLockedWallets();
        var tradeId = Guid.NewGuid();

        var (success, message) = await _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}",
            Quantity, QuoteQuantity, feeBuyer: 0m, feeSeller: 0m);

        Assert.True(success, message);

        // Buyer paid IRT from locked funds: locked consumed, available unchanged (0).
        var buyerQuote = await ReloadAsync(_buyerId, Quote);
        Assert.Equal(0m, buyerQuote.LockedBalance);
        Assert.Equal(0m, buyerQuote.Balance);

        // Buyer received MAUA.
        var buyerBase = await ReloadAsync(_buyerId, Base);
        Assert.Equal(Quantity, buyerBase.Balance);

        // Seller paid MAUA from locked funds.
        var sellerBase = await ReloadAsync(_sellerId, Base);
        Assert.Equal(0m, sellerBase.LockedBalance);
        Assert.Equal(0m, sellerBase.Balance);

        // Seller received IRT.
        var sellerQuote = await ReloadAsync(_sellerId, Quote);
        Assert.Equal(QuoteQuantity, sellerQuote.Balance);

        // Exactly four Trade transaction records, all tied to this trade.
        var txns = await _context.Transactions.Where(t => t.ReferenceId == tradeId.ToString()).ToListAsync();
        Assert.Equal(4, txns.Count);
        Assert.All(txns, t => Assert.Equal(TransactionType.Trade, t.Type));
    }

    [Fact]
    public async Task SettleTradeAsync_CalledTwice_IsIdempotent()
    {
        SeedFullyLockedWallets();
        var tradeId = Guid.NewGuid();

        var first = await _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);
        Assert.True(first.Success, first.Message);

        // Second delivery of the same trade (e.g. an outbox retry after a lost response).
        var second = await _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);
        Assert.True(second.Success, second.Message);

        // Balances must reflect a single settlement, not a double one.
        var buyerBase = await ReloadAsync(_buyerId, Base);
        Assert.Equal(Quantity, buyerBase.Balance); // not 2 * Quantity
        var sellerQuote = await ReloadAsync(_sellerId, Quote);
        Assert.Equal(QuoteQuantity, sellerQuote.Balance); // not 2 * QuoteQuantity

        // Still only four transaction records.
        var count = await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString());
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task SettleTradeAsync_WhenBuyerIsTheSeller_RefusesAndChangesNothing()
    {
        // Matching now blocks self-matching, but settlement guards it too: with one user on
        // both sides the two "different" wallets resolve to the same tracked entity, which
        // produces an incoherent balance history even though the trade nets out.
        SeedFullyLockedWallets();
        var tradeId = Guid.NewGuid();

        var (success, message) = await _repository.SettleTradeAsync(
            tradeId, _buyerId, _buyerId, $"{Base}/{Quote}",
            Quantity, QuoteQuantity, 0m, 0m);

        Assert.False(success, "settlement must refuse a self-trade");
        Assert.Contains("must be different users", message);

        var count = await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString());
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SettleTradeAsync_WithNonZeroFee_RefusesAndChangesNothing()
    {
        // Settlement debits the payer in full but would credit the receiver net of the fee,
        // with the fee going nowhere — so a non-zero fee destroys money. Until fees are
        // credited to a fee account (issue #35), settlement must fail closed rather than leak.
        SeedFullyLockedWallets();
        var tradeId = Guid.NewGuid();

        var (success, message) = await _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}",
            Quantity, QuoteQuantity, feeBuyer: 0.5m, feeSeller: 0m);

        Assert.False(success, "settlement must refuse a non-zero fee");
        Assert.Contains("Fee crediting is not implemented", message);

        // Nothing moved and nothing was recorded.
        var buyerBase = await ReloadAsync(_buyerId, Base);
        Assert.Equal(0m, buyerBase.Balance);
        var sellerBase = await ReloadAsync(_sellerId, Base);
        Assert.Equal(Quantity, sellerBase.LockedBalance);
        var count = await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString());
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SettleTradeAsync_WhenCollateralNotLocked_FailsAndChangesNothing()
    {
        // Buyer's IRT was never locked — simulates the lock-after-match ordering bug (C-5).
        SeedWallet(_buyerId, Quote, balance: 0m, locked: 0m);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();
        var tradeId = Guid.NewGuid();

        var (success, _) = await _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);

        Assert.False(success);

        // Nothing changed: no balances moved, no transaction records written (transaction rolled back).
        var sellerBase = await ReloadAsync(_sellerId, Base);
        Assert.Equal(Quantity, sellerBase.LockedBalance); // still locked
        Assert.Equal(0m, sellerBase.Balance);
        var count = await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString());
        Assert.Equal(0, count);
    }
}
