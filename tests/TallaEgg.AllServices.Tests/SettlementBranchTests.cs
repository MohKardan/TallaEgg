using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The <c>SettleTradeAsync</c> branches the happy path never reaches (issue #46).
///
/// <see cref="SettleTradeAsyncTests"/> covers the successful path, idempotency, self-trading,
/// non-zero fees and unlocked collateral. What this adds is the three remaining branches: a missing
/// wallet on one side, a malformed symbol, and a settlement backed by credit, where the available
/// balance is negative.
///
/// The third branch matters most: until now it had only been seen through manual trading on a
/// development machine. <c>ConsumeLockedBalance</c> exists precisely for it — replace that call
/// with <c>UnLockBalance</c> followed by <c>DecreaseBalance</c> and the non-negative guard in
/// <c>DecreaseBalance</c> fires, failing settlement for every customer in debt, while no other test
/// breaks.
/// </summary>
public class SettlementBranchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WalletDbContext _context;
    private readonly WalletRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Base = "MAUA";
    private const string Quote = "IRT";
    private const decimal Quantity = 2m;
    private const decimal QuoteQuantity = 1000m;

    public SettlementBranchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;
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

    private void SeedFullyLockedWallets()
    {
        SeedWallet(_buyerId, Quote, balance: 0m, locked: QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();
    }

    private async Task<WalletEntity> ReloadAsync(Guid userId, string asset)
    {
        _context.ChangeTracker.Clear();
        return await _context.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == asset);
    }

    private Task<(bool Success, string Message)> SettleAsync(Guid tradeId, string symbol = $"{Base}/{Quote}") =>
        _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, symbol, Quantity, QuoteQuantity, feeBuyer: 0m, feeSeller: 0m);

    // ── Missing wallet ──────────────────────────────────────────────────────────

    /// <summary>
    /// Settlement needs four wallets. The receiving wallet may never have been created — a customer
    /// who has only ever held toman has no gold wallet.
    ///
    /// This is exactly what refused the first purchase of a new symbol in the live environment: the
    /// seller's collateral was already locked, but because the buyer had no wallet for the asset the
    /// whole settlement rolled back with "wallets were not found", leaving collateral locked against
    /// a trade that never completed. The correct behaviour is to create the missing wallet — for a
    /// valid asset — on the spot and settle, the same way registration creates wallets lazily.
    /// </summary>
    [Fact]
    public async Task WhenAParticipantWalletIsMissing_ItIsCreatedAndSettlementSucceeds()
    {
        // The buyer has no gold wallet — the very wallet that should receive the gold.
        SeedWallet(_buyerId, Quote, balance: 0m, locked: QuoteQuantity);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();

        var tradeId = Guid.NewGuid();
        var (success, message) = await SettleAsync(tradeId);

        Assert.True(success, message);

        var buyerBase = await ReloadAsync(_buyerId, Base);
        Assert.Equal(Quantity, buyerBase.Balance);
        var buyerQuote = await ReloadAsync(_buyerId, Quote);
        Assert.Equal(0m, buyerQuote.LockedBalance);
        var sellerBase = await ReloadAsync(_sellerId, Base);
        Assert.Equal(0m, sellerBase.LockedBalance);
        Assert.Equal(0m, sellerBase.Balance);

        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
        Assert.Equal(1, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
    }

    /// <summary>
    /// A malformed symbol whose sides are not real assets — unknown rather than merely missing —
    /// must not create phantom wallets. That is the <c>IsValidCurrency</c> guard, the same one the
    /// admin top-up path applies.
    /// </summary>
    [Fact]
    public async Task WhenAParticipantWalletIsMissingForAnUnknownAsset_NothingMoves()
    {
        SeedWallet(_buyerId, "NOT_A_REAL_ASSET", balance: 0m, locked: QuoteQuantity);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, "NOT_A_REAL_ASSET", balance: 0m, locked: 0m);
        _context.SaveChanges();

        var tradeId = Guid.NewGuid();
        var (success, message) = await SettleAsync(tradeId, $"{Base}/NOT_A_REAL_ASSET");

        Assert.False(success);
        Assert.Contains("wallets were not found", message);

        var sellerBase = await ReloadAsync(_sellerId, Base);
        Assert.Equal(Quantity, sellerBase.LockedBalance);
        Assert.Equal(0m, sellerBase.Balance);

        Assert.Equal(0, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
        Assert.Equal(0, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
    }

    // ── Malformed symbol ────────────────────────────────────────────────────────

    /// <summary>
    /// A symbol must be exactly <c>BASE/QUOTE</c>. The code used to write <c>Split('/')[1]</c>
    /// blindly in places; against a malformed symbol that became an
    /// <c>IndexOutOfRangeException</c>, which the outbox processor read as a failed attempt and
    /// retried five times — retries that could never help, because the data was bad, not the
    /// conditions.
    ///
    /// An explicit refusal makes the same mistake legible on the first attempt.
    /// </summary>
    [Theory]
    [InlineData("MAUA")]        // بدون جداکننده
    [InlineData("MAUA/")]       // نیمهٔ دوم خالی
    [InlineData("/IRT")]        // نیمهٔ اول خالی
    [InlineData("MAUA/IRT/X")]  // سه بخش
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMalformedSymbol_IsRejectedWithoutTouchingAnyWallet(string symbol)
    {
        SeedFullyLockedWallets();
        var tradeId = Guid.NewGuid();

        var (success, message) = await SettleAsync(tradeId, symbol);

        Assert.False(success);
        Assert.Contains("Invalid symbol", message);

        var buyerQuote = await ReloadAsync(_buyerId, Quote);
        Assert.Equal(QuoteQuantity, buyerQuote.LockedBalance);
        Assert.Equal(0, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
    }

    /// <summary>
    /// A valid symbol in lower case and with surrounding spaces must be accepted. Remove the
    /// normalisation and this settlement fails with "wallet not found" — an error whose cause has
    /// nothing to do with wallets.
    /// </summary>
    [Fact]
    public async Task AWellFormedSymbolIsNormalised_SoCaseAndSpacingDoNotBreakSettlement()
    {
        SeedFullyLockedWallets();

        var (success, message) = await SettleAsync(Guid.NewGuid(), " maua / irt ");

        Assert.True(success, message);
        Assert.Equal(Quantity, (await ReloadAsync(_buyerId, Base)).Balance);
    }

    // ── Credit-backed settlement ────────────────────────────────────────────────

    /// <summary>
    /// The buyer holds no toman and buys on credit: locking the collateral already drove their
    /// available balance negative. Settlement must succeed, and the debt must remain on
    /// <c>Balance</c>.
    ///
    /// This is the case <c>ConsumeLockedBalance</c> was written for. The simpler route —
    /// <c>UnLockBalance</c> then <c>DecreaseBalance</c> — fails here, because
    /// <c>DecreaseBalance</c> has a floor of zero and refuses a negative balance. Settlement would
    /// stall for every customer in debt, and since no other test uses a negative balance, nothing
    /// else would catch it.
    /// </summary>
    [Fact]
    public async Task ACreditBackedBuyer_SettlesAndKeepsTheDebtOnTheBalance()
    {
        // The real sequence: a zero-balance wallet, then the collateral lock, which takes it negative.
        var buyerQuote = SeedWallet(_buyerId, Quote, balance: 0m, locked: 0m);
        buyerQuote.LockBalance(QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();

        Assert.Equal(-QuoteQuantity, buyerQuote.Balance); // بدهی پیش از تسویه

        var tradeId = Guid.NewGuid();
        var (success, message) = await SettleAsync(tradeId);

        Assert.True(success, message);

        // The lock was consumed but the debt remains — no money was created.
        var settledBuyerQuote = await ReloadAsync(_buyerId, Quote);
        Assert.Equal(0m, settledBuyerQuote.LockedBalance);
        Assert.Equal(-QuoteQuantity, settledBuyerQuote.Balance);

        // And the buyer actually received the gold.
        Assert.Equal(Quantity, (await ReloadAsync(_buyerId, Base)).Balance);

        // The seller received their toman in full: the buyer being in debt must not reduce it.
        Assert.Equal(QuoteQuantity, (await ReloadAsync(_sellerId, Quote)).Balance);

        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
    }

    /// <summary>
    /// Net value across both sides must stay zero: what the buyer owes is exactly what the seller
    /// received, and the gold the buyer gained is exactly what the seller gave up.
    ///
    /// This is the same check that was previously run by hand in SQL during manual testing.
    /// </summary>
    [Fact]
    public async Task ACreditBackedSettlement_ConservesMoney()
    {
        var buyerQuote = SeedWallet(_buyerId, Quote, balance: 0m, locked: 0m);
        buyerQuote.LockBalance(QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        var sellerBase = SeedWallet(_sellerId, Base, balance: Quantity, locked: 0m);
        sellerBase.LockBalance(Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();

        Assert.True((await SettleAsync(Guid.NewGuid())).Success);

        _context.ChangeTracker.Clear();
        var all = await _context.Wallets.ToListAsync();

        // Available plus locked balance, per asset.
        var gold = all.Where(w => w.Asset == Base).Sum(w => w.Balance + w.LockedBalance);
        var toman = all.Where(w => w.Asset == Quote).Sum(w => w.Balance + w.LockedBalance);

        Assert.Equal(Quantity, gold);  // همان مقداری که فروشنده از اول داشت
        Assert.Equal(0m, toman);       // تومانی وارد سیستم نشده بود
    }
}
