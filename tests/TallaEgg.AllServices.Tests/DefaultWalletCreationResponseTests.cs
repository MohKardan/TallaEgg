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
/// <c>WalletService.CreateDefaultWalletsAsync</c> seeds a new user's Toman, MAUA and CREDIT_MAUA
/// wallets, and is called once per registration. Two defects in what it reported are fixed here
/// (issue #210), both found while reviewing #208.
///
/// <para>
/// <b>The response was built from entities the repository had discarded.</b>
/// <c>WalletRepository.CreateWalletAsync</c> returns the <i>existing</i> row when the user
/// already has that wallet, but the mapper was handed the throwaway new entity instead of the
/// return value. A repeat call for a funded user therefore answered 200 describing an empty
/// wallet: idempotent in the database, but not in what it said. <c>WalletDTO</c> carries no id,
/// so <c>UpdatedAt</c> is the field that says which row was described, and it is asserted
/// alongside the balances.
/// </para>
///
/// <para>
/// <b>Every failure was wrapped in <c>InvalidOperationException</c>.</b> That flattened a SQL
/// timeout, a dropped connection and a bug into one type and one fixed message, made the
/// endpoint's <c>catch (BusinessRuleException)</c> unreachable, and left the real cause reachable
/// only through <c>InnerException</c>. The wrapper is gone, on the precedent set by #134: what
/// <c>GlobalExceptionHandler</c> already handles is not re-handled on the way up.
/// </para>
/// </summary>
public class DefaultWalletCreationResponseTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DefaultWalletCreationResponseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private WalletDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options);

    private static WalletRepository NewRepository(WalletDbContext context) =>
        new(NullLogger<WalletRepository>.Instance, context);

    private static WalletService NewService(IWalletRepository repository) =>
        new(repository, new WalletMapper(), NullLogger<WalletService>.Instance);

    /// <summary>
    /// The defect itself. A second call for a user whose MAUA wallet already holds funds must
    /// describe the stored row, not the discarded one it tried to insert.
    /// </summary>
    [Fact]
    public async Task CreateDefaultWalletsAsync_UserAlreadyHasAFundedWallet_ReportsTheStoredRow()
    {
        using var context = NewContext();
        var repository = NewRepository(context);
        var service = NewService(repository);
        var userId = Guid.NewGuid();

        await service.CreateDefaultWalletsAsync(userId);
        await service.DepositAsync(userId, CurrenciesConstant.Maua, 100m);
        await repository.LockBalanceAsync(userId, CurrenciesConstant.Maua, 40m);

        var second = await service.CreateDefaultWalletsAsync(userId);

        using var verify = NewContext();
        var stored = await verify.Wallets.SingleAsync(
            w => w.UserId == userId && w.Asset == CurrenciesConstant.Maua);

        var reported = Assert.Single(second, w => w.Asset == CurrenciesConstant.Maua);
        Assert.Equal(60m, reported.Balance);
        Assert.Equal(40m, reported.LockedBalance);

        // What identifies the row, in the absence of an id on the DTO: a freshly built
        // WalletEntity carries DateTime.UtcNow, never the stored row's timestamp.
        Assert.Equal(stored.UpdatedAt, reported.UpdatedAt);
    }

    /// <summary>
    /// The other two default wallets are untouched by the deposit, so their stored balance is
    /// zero either way. Asserted so the test above cannot pass on a service that reports the
    /// deposit against every wallet it returns.
    /// </summary>
    [Fact]
    public async Task CreateDefaultWalletsAsync_CalledTwice_ReportsAllThreeDefaultWallets()
    {
        using var context = NewContext();
        var service = NewService(NewRepository(context));
        var userId = Guid.NewGuid();

        await service.CreateDefaultWalletsAsync(userId);
        await service.DepositAsync(userId, CurrenciesConstant.Maua, 100m);

        var second = (await service.CreateDefaultWalletsAsync(userId)).ToList();

        Assert.Equal(
            new[] { CurrenciesConstant.Toman, CurrenciesConstant.Maua, CurrenciesConstant.Credit_MAUA },
            second.Select(w => w.Asset));
        Assert.Equal(0m, Assert.Single(second, w => w.Asset == CurrenciesConstant.Toman).Balance);
        Assert.Equal(0m, Assert.Single(second, w => w.Asset == CurrenciesConstant.Credit_MAUA).Balance);
    }

    /// <summary>
    /// The database side was already idempotent and stays that way. The fix is to what the call
    /// reports, not to what it writes.
    /// </summary>
    [Fact]
    public async Task CreateDefaultWalletsAsync_CalledTwice_CreatesNoDuplicateRows()
    {
        using var context = NewContext();
        var service = NewService(NewRepository(context));
        var userId = Guid.NewGuid();

        await service.CreateDefaultWalletsAsync(userId);
        await service.CreateDefaultWalletsAsync(userId);

        using var verify = NewContext();
        Assert.Equal(3, await verify.Wallets.CountAsync(w => w.UserId == userId));
    }

    /// <summary>
    /// A first call for a new user still returns three empty wallets whose timestamps are the
    /// stored ones. This is the path where the entity handed to the repository and the row it
    /// returns are the same object, and the only path the single caller exercises today.
    /// </summary>
    [Fact]
    public async Task CreateDefaultWalletsAsync_NewUser_ReturnsTheThreeWalletsItJustCreated()
    {
        using var context = NewContext();
        var service = NewService(NewRepository(context));
        var userId = Guid.NewGuid();

        var created = (await service.CreateDefaultWalletsAsync(userId)).ToList();

        using var verify = NewContext();
        var stored = await verify.Wallets.Where(w => w.UserId == userId).ToListAsync();

        Assert.Equal(3, created.Count);
        Assert.All(created, w => Assert.Equal(0m, w.Balance));
        Assert.Equal(
            stored.Select(w => w.UpdatedAt).OrderBy(t => t),
            created.Select(w => w.UpdatedAt).OrderBy(t => t));
    }

    /// <summary>
    /// Whatever fails underneath now reaches the caller as itself. Before the fix every one of
    /// these arrived as an <c>InvalidOperationException</c> carrying one fixed message, so
    /// <c>GlobalExceptionHandler</c> logged that as the exception and the real cause sat one
    /// level down in <c>InnerException</c>.
    /// </summary>
    [Fact]
    public async Task CreateDefaultWalletsAsync_RepositoryFails_PropagatesTheOriginalException()
    {
        var service = NewService(new ThrowingRepository());

        var thrown = await Assert.ThrowsAsync<StorageUnavailableException>(
            () => service.CreateDefaultWalletsAsync(Guid.NewGuid()));

        Assert.Equal("the storage layer is unavailable", thrown.Message);
        Assert.Null(thrown.InnerException);
    }

    /// <summary>Stands in for a database that is refusing writes.</summary>
    private sealed class StorageUnavailableException(string message) : Exception(message);

    /// <summary>
    /// Fails the one call <c>CreateDefaultWalletsAsync</c> makes. Every other member throws
    /// rather than returning a plausible default, so a test that strays outside the path under
    /// test fails loudly instead of quietly passing.
    /// </summary>
    private sealed class ThrowingRepository : IWalletRepository
    {
        public Task<WalletEntity> CreateWalletAsync(WalletEntity wallet) =>
            throw new StorageUnavailableException("the storage layer is unavailable");

        public Task<WalletEntity?> GetWalletAsync(Guid userId, string asset) => throw new NotImplementedException();
        public Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId) => throw new NotImplementedException();
        public Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet, Transaction? transaction = null) => throw new NotImplementedException();
        public Task<WalletEntity> LockBalanceAsync(Guid userId, string asset, decimal amount) => throw new NotImplementedException();
        public Task<WalletEntity> UnlockBalanceAsync(Guid userId, string asset, decimal amount) => throw new NotImplementedException();
        public Task<Transaction> CreateTransactionAsync(Transaction transaction) => throw new NotImplementedException();
        public Task<Transaction?> FindTransactionByReferenceAsync(Guid walletId, string referenceId) => throw new NotImplementedException();
        public Task<Transaction> ApplyWithIdempotencyAsync(WalletEntity wallet, Transaction transaction) => throw new NotImplementedException();
        public Task<WalletTransaction?> GetTransactionAsync(Guid transactionId) => throw new NotImplementedException();
        public Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null) => throw new NotImplementedException();
        public Task<IEnumerable<WalletTransaction>> GetTransactionsByReferenceAsync(string referenceId) => throw new NotImplementedException();
        public Task<WalletTransaction> UpdateTransactionAsync(WalletTransaction transaction) => throw new NotImplementedException();
        public Task<(bool Success, string Message)> SettleTradeAsync(
            Guid tradeId, Guid buyerUserId, Guid sellerUserId,
            string symbol, decimal quantity, decimal quoteQuantity,
            decimal feeBuyer, decimal feeSeller) => throw new NotImplementedException();
    }
}
