using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Issue #282: <see cref="WalletRepository.GetWalletAsync"/> previously applied <c>ToUpper()</c>
/// to the database column (<c>w.Asset.ToUpper()</c>), translating to <c>UPPER([Asset])</c> and
/// preventing SQL Server from seeking on the trailing key column of <c>IX_Wallets_UserId_Asset</c>.
///
/// <para>
/// Normalizing <see cref="WalletEntity.Asset"/> on write in <see cref="WalletEntity.Create"/>
/// (matching <see cref="Transaction.Create"/>) and normalizing the lookup parameter before the
/// query keeps lookups case-insensitive on both SQL Server and SQLite while leaving the column
/// predicate sargable.
/// </para>
/// </summary>
public class WalletAssetLookupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CommandCapture _commands = new();

    public WalletAssetLookupTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Theory]
    [InlineData(" maua ", "MAUA")]
    [InlineData("irt", "IRT")]
    [InlineData("Maua", "MAUA")]
    [InlineData("  credit_maua\t", "CREDIT_MAUA")]
    public void Create_NormalizesAssetToTrimmedUppercase(string rawAsset, string expected)
    {
        var wallet = WalletEntity.Create(Guid.NewGuid(), rawAsset);

        Assert.Equal(expected, wallet.Asset);
    }

    [Theory]
    [InlineData("maua")]
    [InlineData("MAUA")]
    [InlineData("Maua")]
    [InlineData("  maua  ")]
    public async Task GetWalletAsync_FindsWalletRegardlessOfInputCaseOrPadding(string lookupAsset)
    {
        var userId = Guid.NewGuid();
        await SeedWalletAsync(userId, " maua ");

        await using var context = NewContext();
        var repository = new WalletRepository(NullLogger<WalletRepository>.Instance, context);

        var found = await repository.GetWalletAsync(userId, lookupAsset);

        Assert.NotNull(found);
        Assert.Equal(userId, found.UserId);
        Assert.Equal("MAUA", found.Asset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetWalletAsync_ReturnsNullForWhitespaceAsset(string blankAsset)
    {
        var userId = Guid.NewGuid();
        await SeedWalletAsync(userId, "MAUA");

        await using var context = NewContext();
        var repository = new WalletRepository(NullLogger<WalletRepository>.Instance, context);

        var found = await repository.GetWalletAsync(userId, blankAsset);

        Assert.Null(found);
    }

    /// <summary>
    /// Asserts directly on the SQL executed by <see cref="WalletRepository.GetWalletAsync"/> so
    /// reintroducing <c>w.Asset.ToUpper()</c> (or any other <c>UPPER(...)</c> wrapper on the
    /// column) breaks this test even when the functional lookup still returns the right row.
    /// </summary>
    [Fact]
    public async Task GetWalletAsync_EmitsSargableAssetPredicateWithoutUpperOnColumn()
    {
        var userId = Guid.NewGuid();
        await SeedWalletAsync(userId, "MAUA");
        _commands.Texts.Clear();

        await using var context = NewContext();
        var repository = new WalletRepository(NullLogger<WalletRepository>.Instance, context);

        var found = await repository.GetWalletAsync(userId, "maua");
        Assert.NotNull(found);

        var walletWhereClauses = _commands.Texts
            .Where(t => t.Contains("FROM \"Wallets\"", StringComparison.OrdinalIgnoreCase))
            .Select(t =>
            {
                var idx = t.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
                return idx >= 0 ? t[idx..] : string.Empty;
            })
            .ToList();

        Assert.NotEmpty(walletWhereClauses);
        Assert.All(walletWhereClauses, where =>
        {
            Assert.Contains("\"UserId\"", where);
            Assert.Contains("\"Asset\"", where);
            Assert.DoesNotContain("UPPER(", where, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async Task SeedWalletAsync(Guid userId, string asset)
    {
        await using var context = NewContext();
        context.Wallets.Add(WalletEntity.Create(userId, asset));
        await context.SaveChangesAsync();
    }

    private WalletDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WalletDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_commands)
            .Options);

    private sealed class CommandCapture : DbCommandInterceptor
    {
        public List<string> Texts { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Texts.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
