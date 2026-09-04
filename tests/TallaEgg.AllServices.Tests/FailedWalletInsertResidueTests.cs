using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// An insert that loses the duplicate race must not stay pending on the context.
///
/// <para>
/// <c>WalletRepository.CreateWalletAsync</c> absorbs a lost race by catching, re-reading, and
/// returning the row the winner wrote. EF only calls <c>AcceptAllChanges</c> when
/// <c>SaveChangesAsync</c> succeeds, so the entity whose insert failed stayed in state
/// <c>Added</c> on the <c>DbContext</c> — which is scoped per request and shared by all three
/// creates <c>WalletService.CreateDefaultWalletsAsync</c> makes, and by the read-modify-write in
/// <c>LockBalanceAsync</c>. The next <c>SaveChangesAsync</c> flushed the doomed insert alongside
/// whatever it was actually for, the unique index on <c>(UserId, Asset)</c> rejected it again,
/// and the whole batch rolled back — taking the good row with it (issue #223).
/// </para>
///
/// <para>
/// The collision is driven through a context that lets a competitor commit immediately before its
/// own save, the same way <c>WalletConcurrencyTests</c> does it. Racing real threads would make
/// the collision a matter of timing, so the test would become the flake rather than the proof.
/// </para>
/// </summary>
public class FailedWalletInsertResidueTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public FailedWalletInsertResidueTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<WalletDbContext> Options() =>
        new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;

    private WalletDbContext NewContext() => new(Options());

    private static WalletRepository NewRepository(WalletDbContext context) =>
        new(NullLogger<WalletRepository>.Instance, context);

    private static WalletService NewService(WalletDbContext context) =>
        new(NewRepository(context), new WalletMapper(), NullLogger<WalletService>.Instance);

    /// <summary>
    /// Commits <paramref name="asset"/> for <paramref name="userId"/> from another context, once,
    /// the first time the context under test is about to save. That is the window the pre-check in
    /// <c>CreateWalletAsync</c> cannot close: it has already run and found nothing.
    /// </summary>
    private Action CompetitorWinsOnce(Guid userId, string asset, Action? onRun = null)
    {
        var ran = false;

        return () =>
        {
            if (ran) return;
            ran = true;

            using var competitor = NewContext();
            competitor.Wallets.Add(WalletEntity.Create(userId, asset));
            competitor.SaveChanges();

            onRun?.Invoke();
        };
    }

    /// <summary>
    /// The defect itself, at the surface it was found on. Losing the race on the first of the
    /// three default wallets must not cost the user the other two: before the fix the failed IRT
    /// insert rode along with the MAUA insert, the batch rolled back, and the call ended in a 500
    /// with one wallet on the account instead of three.
    /// </summary>
    [Fact]
    public async Task CreateDefaultWalletsAsync_LosesTheRaceOnTheFirstWallet_StillCreatesTheOtherTwo()
    {
        var userId = Guid.NewGuid();
        var competitorRan = false;

        await using var context = new CollidingContext(Options());
        context.BeforeSave = CompetitorWinsOnce(userId, CurrenciesConstant.Toman, () => competitorRan = true);

        var reported = (await NewService(context).CreateDefaultWalletsAsync(userId)).ToList();

        Assert.True(competitorRan, "the competitor never ran, so no insert actually lost a race");

        await using var verify = NewContext();
        var stored = await verify.Wallets.Where(w => w.UserId == userId).Select(w => w.Asset).ToListAsync();

        Assert.Equal(
            Expected().OrderBy(a => a, StringComparer.Ordinal),
            stored.OrderBy(a => a, StringComparer.Ordinal));

        // And the caller is told about all three, including the one the competitor wrote.
        Assert.Equal(Expected(), reported.Select(w => w.Asset));

        static string[] Expected() =>
            [CurrenciesConstant.Toman, CurrenciesConstant.Maua, CurrenciesConstant.Credit_MAUA];
    }

    /// <summary>
    /// The mechanism underneath, pinned on its own so a future change that fixes the symptom some
    /// other way still has to leave the context clean. A caller that gets a wallet back has no way
    /// to know an insert failed, so it is the repository's job not to leave one behind.
    /// </summary>
    [Fact]
    public async Task CreateWalletAsync_LosesTheDuplicateRace_LeavesNoPendingInsertBehind()
    {
        var userId = Guid.NewGuid();

        await using var context = new CollidingContext(Options());
        context.BeforeSave = CompetitorWinsOnce(userId, CurrenciesConstant.Toman);

        var mine = WalletEntity.Create(userId, CurrenciesConstant.Toman);
        var returned = await NewRepository(context).CreateWalletAsync(mine);

        // The winner's row came back, which is the behaviour the catch exists for.
        Assert.NotEqual(mine.Id, returned.Id);
        Assert.Equal(CurrenciesConstant.Toman, returned.Asset);

        Assert.DoesNotContain(context.ChangeTracker.Entries<WalletEntity>(),
            e => e.State == EntityState.Added);
    }

    /// <summary>
    /// The same residue reaches trading, not just registration. <c>LockBalanceAsync</c> creates a
    /// missing wallet lazily and then writes to it through the very same context, so a lost race
    /// on the create took the collateral lock down with it — a refused trade rather than a
    /// half-seeded account.
    /// </summary>
    [Fact]
    public async Task LockBalanceAsync_LosesTheRaceCreatingTheWallet_StillLocksAgainstTheStoredRow()
    {
        var userId = Guid.NewGuid();

        await using var context = new CollidingContext(Options());
        context.BeforeSave = CompetitorWinsOnce(userId, CurrenciesConstant.Toman);

        await NewRepository(context).LockBalanceAsync(userId, CurrenciesConstant.Toman, 10m);

        await using var verify = NewContext();
        var stored = await verify.Wallets.SingleAsync(w => w.UserId == userId);

        Assert.Equal(10m, stored.LockedBalance);
        Assert.Equal(-10m, stored.Balance);
    }

    /// <summary>
    /// A context that runs a delegate immediately before its own SaveChanges, to open the window a
    /// competing writer commits in. Deliberately a copy of the one in
    /// <c>WalletConcurrencyTests</c> rather than an extraction of it: that file is about the
    /// optimistic-concurrency token, this one is about change-tracker residue, and promoting a
    /// ten-line test double into shared scaffolding is a refactor these tests do not need.
    /// </summary>
    private sealed class CollidingContext(DbContextOptions<WalletDbContext> options) : WalletDbContext(options)
    {
        public Action? BeforeSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            BeforeSave?.Invoke();
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
