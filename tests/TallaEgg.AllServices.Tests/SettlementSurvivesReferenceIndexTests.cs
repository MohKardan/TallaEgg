using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The trap the issue warns whoever implements #157 about, pinned as a test.
///
/// <para>
/// Settling one trade writes <b>four</b> transaction rows under a single reference — the trade id
/// (<c>WalletRepository.SettleTradeAsync</c>): buyer pays quote, buyer receives base, seller pays
/// base, seller receives quote. A unique index on <c>ReferenceId</c> alone would reject the second
/// of those four and break every trade in the system.
/// </para>
///
/// <para>
/// The index added for #157 is over <c>(WalletId, ReferenceId)</c>, and the four legs touch four
/// different wallets, because settlement refuses a trade where buyer and seller are the same user.
/// Verified on the live local database before the index was written — every reference in
/// <c>Transactions</c> was distinct under that pair — and verified here against the constraint
/// itself, which is the part that would actually fail.
/// </para>
/// </summary>
public class SettlementSurvivesReferenceIndexTests : IDisposable
{
    private const string Base = "MAUA";
    private const string Quote = "IRT";
    private const decimal Quantity = 2m;
    private const decimal QuoteQuantity = 1000m;

    private readonly SqliteConnection _connection;
    private readonly WalletDbContext _context;
    private readonly WalletRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    public SettlementSurvivesReferenceIndexTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new WalletDbContext(
            new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);

        Seed(_buyerId, Quote, locked: QuoteQuantity);
        Seed(_buyerId, Base, locked: 0m);
        Seed(_sellerId, Base, locked: Quantity);
        Seed(_sellerId, Quote, locked: 0m);
        _context.SaveChanges();
    }

    private void Seed(Guid userId, string asset, decimal locked)
    {
        var wallet = WalletEntity.Create(userId, asset);
        wallet.LockedBalance = locked;
        _context.Wallets.Add(wallet);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// The regression test for the index: a trade settles, and all four legs are written under the
    /// one trade id. If the index were over ReferenceId alone this would fail on the second leg
    /// and roll the whole settlement back.
    /// </summary>
    [Fact]
    public async Task ATradeStillSettlesAndWritesItsFourLegsUnderOneReference()
    {
        var tradeId = Guid.NewGuid();

        var (success, message) = await _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);

        Assert.True(success, message);

        _context.ChangeTracker.Clear();
        var legs = await _context.Transactions
            .Where(t => t.ReferenceId == tradeId.ToString())
            .ToListAsync();

        Assert.Equal(4, legs.Count);
        Assert.Equal(4, legs.Select(l => l.WalletId).Distinct().Count());
    }

    /// <summary>
    /// And settlement stays idempotent, which is the behaviour #157 was asked to copy. Proving it
    /// still holds with the new index in place matters: the second settlement must go on returning
    /// success without writing a fifth leg or a duplicate of any of the four.
    /// </summary>
    [Fact]
    public async Task SettlingTheSameTradeTwiceStillSucceedsAndAddsNoLegs()
    {
        var tradeId = Guid.NewGuid();

        await _repository.SettleTradeAsync(tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);
        var (success, message) = await _repository.SettleTradeAsync(tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);

        Assert.True(success, message);

        _context.ChangeTracker.Clear();
        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
        Assert.Equal(1, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
    }

    /// <summary>
    /// Two separate trades between the same pair each write their own four legs. Nothing about the
    /// index is per-wallet-total; it is per wallet and reference.
    /// </summary>
    [Fact]
    public async Task TwoTradesBetweenTheSamePairEachWriteTheirOwnLegs()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await _repository.SettleTradeAsync(first, _buyerId, _sellerId, $"{Base}/{Quote}", 1m, 500m, 0m, 0m);
        await _repository.SettleTradeAsync(second, _buyerId, _sellerId, $"{Base}/{Quote}", 1m, 500m, 0m, 0m);

        _context.ChangeTracker.Clear();
        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == first.ToString()));
        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == second.ToString()));
    }
}
