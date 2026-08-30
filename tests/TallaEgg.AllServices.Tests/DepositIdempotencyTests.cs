using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Wallet;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A deposit or withdrawal carrying a reference already applied to a wallet must move nothing and
/// still report success (issue #157).
///
/// <para>
/// The audit reproduced the defect by execution: <c>IncreaseBalanceAsync</c> twice with one
/// reference left a balance of 2,000,000 instead of 1,000,000 and two transaction rows sharing
/// that reference. Nothing checked whether the reference had been used before.
/// </para>
///
/// <para>
/// Modelled on settlement, which already solves the same problem: a repeat is a success that does
/// nothing, never an error an admin has to interpret. The pre-check is the optimisation; the
/// unique index over <c>(WalletId, ReferenceId)</c> is the guarantee, so both are exercised here.
/// </para>
/// </summary>
public class DepositIdempotencyTests : IDisposable
{
    private const string Asset = CurrenciesConstant.Toman;

    private readonly SqliteConnection _connection;
    private readonly HookedWalletDbContext _context;
    private readonly WalletRepository _repository;
    private readonly WalletService _service;
    private readonly Guid _userId = Guid.NewGuid();

    /// <summary>
    /// A hook just before SaveChanges, so the "a competitor committed between our check and our
    /// insert" case can be reproduced deterministically. Same device, and the same reasoning, as
    /// <see cref="SettlementUniquenessTests"/>: in-memory SQLite on one connection cannot run two
    /// concurrent write transactions, so a genuinely parallel test would deadlock or go flaky.
    /// What needs proving is that when the race happens the database stops it and the code reads
    /// that correctly.
    /// </summary>
    private sealed class HookedWalletDbContext(DbContextOptions<WalletDbContext> options) : WalletDbContext(options)
    {
        public Action? BeforeSave;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var hook = BeforeSave;
            BeforeSave = null;
            hook?.Invoke();
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    public DepositIdempotencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;
        _context = new HookedWalletDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);
        _service = new WalletService(_repository, new WalletMapper());

        _context.Wallets.Add(WalletEntity.Create(_userId, Asset));
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<decimal> BalanceAsync()
    {
        _context.ChangeTracker.Clear();
        return (await _context.Wallets.SingleAsync(w => w.UserId == _userId && w.Asset == Asset)).Balance;
    }

    private async Task<int> TransactionCountAsync(string referenceId)
    {
        _context.ChangeTracker.Clear();
        return await _context.Transactions.CountAsync(t => t.ReferenceId == referenceId);
    }

    // ---------------------------------------------------------------- deposits

    /// <summary>The exact scenario the audit reproduced, now with the opposite outcome.</summary>
    [Fact]
    public async Task TheSameDepositReferenceTwiceCreditsOnce()
    {
        const string reference = "admin-deposit:once";

        await _service.DepositAsync(_userId, Asset, 1_000_000m, reference);
        await _service.DepositAsync(_userId, Asset, 1_000_000m, reference);

        Assert.Equal(1_000_000m, await BalanceAsync());
        Assert.Equal(1, await TransactionCountAsync(reference));
    }

    /// <summary>
    /// The repeat reports success, not an error. An admin who re-sent because they saw no
    /// confirmation the first time gets the same confirmation they should have got, showing the
    /// balance the charge actually produced.
    /// </summary>
    [Fact]
    public async Task TheRepeatSucceedsAndReportsTheOriginalBalances()
    {
        const string reference = "admin-deposit:reports";

        var first = await _service.DepositAsync(_userId, Asset, 250m, reference);
        var repeat = await _service.DepositAsync(_userId, Asset, 250m, reference);

        Assert.Equal(first.BalanceBefore, repeat.BalanceBefore);
        Assert.Equal(first.BalanceAfter, repeat.BalanceAfter);
        Assert.Equal(first.TrackingCode, repeat.TrackingCode);
    }

    /// <summary>
    /// The wallet handed back describes the wallet as stored, not as this call would have left it.
    /// A caller applies its balance change before the repository sees it — IncreaseBalance adjusts
    /// Balance and stamps UpdatedAt — and on the idempotent path that change is never saved, so
    /// without discarding it the endpoint would answer with a balance and a modification time the
    /// wallet never had.
    /// </summary>
    [Fact]
    public async Task TheRepeatReportsTheWalletAsStoredRatherThanAsItWouldHaveBeen()
    {
        const string reference = "admin-deposit:reload";

        await _service.DepositAsync(_userId, Asset, 700m, reference);

        var storedUpdatedAt = (await _context.Wallets
            .AsNoTracking()
            .SingleAsync(w => w.UserId == _userId && w.Asset == Asset)).UpdatedAt;

        var (walletAfterRepeat, _) = await _service.IncreaseBalanceAsync(_userId, Asset, 700m, reference);

        Assert.Equal(700m, walletAfterRepeat.Balance);
        Assert.Equal(storedUpdatedAt, walletAfterRepeat.UpdatedAt);
    }

    /// <summary>Two genuinely different charges must both land; deduplication is not a rate limit.</summary>
    [Fact]
    public async Task TwoDepositsWithDifferentReferencesBothCredit()
    {
        await _service.DepositAsync(_userId, Asset, 100m, "admin-deposit:a");
        await _service.DepositAsync(_userId, Asset, 100m, "admin-deposit:b");

        Assert.Equal(200m, await BalanceAsync());
    }

    /// <summary>
    /// Nothing can be deduplicated without a key, and callers that send none must keep working
    /// exactly as before. On SQL Server this is also what the index filter is for: NULLs count as
    /// equal there, so an unfiltered unique index would allow only one of these per wallet.
    /// </summary>
    [Fact]
    public async Task DepositsWithNoReferenceAreAllApplied()
    {
        await _service.DepositAsync(_userId, Asset, 100m);
        await _service.DepositAsync(_userId, Asset, 100m);
        await _service.DepositAsync(_userId, Asset, 100m);

        Assert.Equal(300m, await BalanceAsync());
    }

    /// <summary>
    /// The database is the guarantee, not the pre-check. A competitor commits the same reference
    /// in the window between this caller's check and its insert; the unique index rejects the
    /// insert, and because the balance change and the transaction are one SaveChanges, the
    /// balance is left untouched too.
    /// </summary>
    [Fact]
    public async Task AConcurrentDuplicateIsRejectedByTheDatabaseAndMovesNothing()
    {
        const string reference = "admin-deposit:race";

        var wallet = await _context.Wallets.SingleAsync(w => w.UserId == _userId && w.Asset == Asset);

        _context.BeforeSave = () =>
        {
            // The competitor, writing straight to the database on a second context so that this
            // caller's own change tracker knows nothing about it — exactly what losing the race
            // looks like from inside.
            using var competitor = new WalletDbContext(
                new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options);

            competitor.Transactions.Add(Transaction.Create(
                wallet.Id, 500m, Asset, TransactionType.Deposit, 0m, 500m, null,
                TransactionStatus.Completed, "competitor", reference, null));

            competitor.SaveChanges();
        };

        var result = await _service.DepositAsync(_userId, Asset, 500m, reference);

        Assert.Equal(500m, result.BalanceAfter);
        Assert.Equal(0m, await BalanceAsync());          // the losing attempt moved nothing
        Assert.Equal(1, await TransactionCountAsync(reference));
    }

    // ---------------------------------------------------------------- withdrawals

    /// <summary>
    /// A withdrawal has the same lost-response failure as a deposit, and it costs the customer
    /// rather than the shop, so it is closed the same way.
    /// </summary>
    [Fact]
    public async Task TheSameWithdrawalReferenceTwiceDeductsOnce()
    {
        const string reference = "admin-withdrawal:once";

        await _service.DepositAsync(_userId, Asset, 1_000m);
        await _service.WithdrawalAsync(_userId, Asset, 400m, reference);
        await _service.WithdrawalAsync(_userId, Asset, 400m, reference);

        Assert.Equal(600m, await BalanceAsync());
        Assert.Equal(1, await TransactionCountAsync(reference));
    }

    /// <summary>
    /// A deposit and a withdrawal that happened to arrive with the same reference are opposite
    /// movements, and the index is per wallet and reference rather than per operation, so the
    /// second would be swallowed. The keys are namespaced apart for exactly this reason — pinned
    /// here so the index's blind spot stays visible next to the thing that compensates for it.
    /// </summary>
    [Fact]
    public async Task KeysAreNamespacedSoAWithdrawalIsNeverMistakenForItsDeposit()
    {
        var userId = Guid.NewGuid();
        var at = DateTime.UtcNow;

        Assert.NotEqual(
            TallaEgg.Core.Utilties.AdminAdjustmentKey.ForDeposit(userId, Asset, 100m, at),
            TallaEgg.Core.Utilties.AdminAdjustmentKey.ForWithdrawal(userId, Asset, 100m, at));

        await Task.CompletedTask;
    }
}
