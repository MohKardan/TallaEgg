using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.Enums.Wallet;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Two writers must not be able to overwrite each other's balance (audit finding C-4).
///
/// <para>
/// <c>WalletRepository</c> caught <c>DbUpdateConcurrencyException</c> and logged it, which reads
/// as "this path is protected against races". It was not: EF only raises that exception when a
/// concurrency token is configured, and the Wallet service had none — no <c>RowVersion</c>, no
/// <c>[Timestamp]</c>, no <c>IsConcurrencyToken</c>. Every UPDATE was <c>WHERE Id = @id</c>, which
/// always matches, so two writers both succeeded and the second erased the first. Orders had its
/// token since #74; wallets did not, and the symmetry made the gap easy to miss.
/// </para>
///
/// <para>
/// <b>Why a counter and not <c>rowversion</c>:</b> SQL Server maintains <c>rowversion</c> itself,
/// which cannot be forgotten and would be the stronger choice — but these tests run on SQLite,
/// where EF maps it to a BLOB it never updates. The token would never change, no conflict would
/// ever be detected, and the test below would pass while proving nothing. A counter incremented by
/// the entity behaves the same on both providers, so what production relies on is what is tested.
/// </para>
/// </summary>
public class WalletConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public WalletConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var schema = NewContext();
        schema.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private WalletDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options);

    private WalletRepository NewRepository(WalletDbContext context) =>
        new(NullLogger<WalletRepository>.Instance, context);

    private async Task<WalletEntity> SeedWalletAsync(decimal balance)
    {
        await using var context = NewContext();
        var wallet = WalletEntity.Create(Guid.NewGuid(), "IRT");
        wallet.IncreaseBalance(balance);
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync();
        return wallet;
    }

    // ── The defect ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The exact shape of C-4: two contexts read the same balance, both compute from it, and both
    /// save. Before the token, the second write landed and the first one's withdrawal vanished.
    ///
    /// <para>
    /// Two separate <see cref="WalletDbContext"/> instances over one connection is what makes this
    /// a real race rather than a simulated one — a single context would return the same tracked
    /// instance to both readers and there would be nothing to conflict.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TwoWritersFromTheSameRead_CannotBothSucceed()
    {
        var seeded = await SeedWalletAsync(100m);

        await using var first = NewContext();
        await using var second = NewContext();

        var byFirst = await first.Wallets.SingleAsync(w => w.Id == seeded.Id);
        var bySecond = await second.Wallets.SingleAsync(w => w.Id == seeded.Id);

        Assert.Equal(100m, byFirst.Balance);
        Assert.Equal(100m, bySecond.Balance);

        byFirst.DecreaseBalance(30m);
        await first.SaveChangesAsync();

        bySecond.DecreaseBalance(50m);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var check = NewContext();
        var stored = await check.Wallets.SingleAsync(w => w.Id == seeded.Id);

        // 70, not 50. Without the token the second write won and the first withdrawal was lost.
        Assert.Equal(70m, stored.Balance);
    }

    /// <summary>
    /// The token has to move on a lock as well as on a balance change. <c>LockBalance</c> alters
    /// both fields, but a token placed on <c>Balance</c> alone would have left operations that
    /// only touch <c>LockedBalance</c> — settlement's <c>ConsumeLockedBalance</c> — unprotected.
    /// </summary>
    [Fact]
    public async Task ConsumingLockedCollateral_AlsoAdvancesTheToken()
    {
        var seeded = await SeedWalletAsync(100m);

        await using var setup = NewContext();
        var wallet = await setup.Wallets.SingleAsync(w => w.Id == seeded.Id);
        wallet.LockBalance(40m);
        await setup.SaveChangesAsync();

        await using var first = NewContext();
        await using var second = NewContext();
        var byFirst = await first.Wallets.SingleAsync(w => w.Id == seeded.Id);
        var bySecond = await second.Wallets.SingleAsync(w => w.Id == seeded.Id);

        byFirst.ConsumeLockedBalance(40m);
        await first.SaveChangesAsync();

        bySecond.ConsumeLockedBalance(40m);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var check = NewContext();
        Assert.Equal(0m, (await check.Wallets.SingleAsync(w => w.Id == seeded.Id)).LockedBalance);
    }

    // ── The retry ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Losing the race must not lose the operation. The repository re-reads and recomputes, so a
    /// lock that collided with another writer still ends up applied — to the balance as it is
    /// after that writer, not as it was before.
    ///
    /// <para>
    /// The competitor commits between the repository's read and its save, which is the window the
    /// token exists to close. Doing it through a SaveChanges interceptor makes the collision
    /// deterministic; a genuinely parallel version would depend on timing and would be flaky.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ALockThatLosesTheRace_IsStillApplied()
    {
        var seeded = await SeedWalletAsync(100m);
        var collided = false;

        await using var context = new CollidingContext(
            new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options);

        context.BeforeSave = () =>
        {
            if (collided) return;
            collided = true;

            // A different writer takes 25 out of the wallet and commits first.
            using var competitor = NewContext();
            var theirs = competitor.Wallets.Single(w => w.Id == seeded.Id);
            theirs.DecreaseBalance(25m);
            competitor.SaveChanges();
        };

        var repository = NewRepository(context);
        await repository.LockBalanceAsync(seeded.UserId, "IRT", 10m);

        Assert.True(collided, "the competitor never ran, so nothing was actually retried");

        await using var check = NewContext();
        var stored = await check.Wallets.SingleAsync(w => w.Id == seeded.Id);

        // 100 − 25 by the competitor, then − 10 moved into the lock by the retried operation.
        Assert.Equal(65m, stored.Balance);
        Assert.Equal(10m, stored.LockedBalance);
    }

    // ── The counter's one weakness ──────────────────────────────────────────────

    /// <summary>
    /// A database-maintained <c>rowversion</c> cannot be forgotten; a counter can. Every public
    /// method that changes a balance must advance <see cref="WalletEntity.Version"/>, and this
    /// covers the ones nobody has written yet — which is the only coverage that would have caught
    /// the original gap, since C-4 was a missing mechanism rather than a mistaken one.
    /// </summary>
    [Fact]
    public void EveryBalanceMutator_AdvancesTheToken()
    {
        var mutators = typeof(WalletEntity)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            // IsSpecialName excludes the property setters, which also take one decimal but are
            // not mutators in this sense. That they exist at all is its own smell — a caller can
            // assign Balance directly and bypass every guard — but narrowing them is a separate
            // change from making the token reliable.
            .Where(m => !m.IsSpecialName
                     && m.GetParameters().Length == 1
                     && m.GetParameters()[0].ParameterType == typeof(decimal))
            .ToList();

        Assert.NotEmpty(mutators);

        var stale = new List<string>();
        foreach (var mutator in mutators)
        {
            var wallet = WalletEntity.Create(Guid.NewGuid(), "IRT");
            wallet.IncreaseBalance(1000m);
            wallet.LockBalance(500m);

            var before = wallet.Version;
            mutator.Invoke(wallet, new object[] { 1m });

            if (wallet.Version == before) stale.Add(mutator.Name);
        }

        Assert.True(stale.Count == 0,
            "These change a balance without advancing Version, so a concurrent writer's UPDATE " +
            "would not notice them:" + Environment.NewLine +
            string.Join(Environment.NewLine, stale.Select(n => "  WalletEntity." + n)));
    }

    /// <summary>
    /// A context that runs a delegate immediately before its own SaveChanges, to open the window a
    /// competing writer commits in.
    /// </summary>
    private sealed class CollidingContext : WalletDbContext
    {
        public CollidingContext(DbContextOptions<WalletDbContext> options) : base(options) { }

        public Action? BeforeSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            BeforeSave?.Invoke();
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
