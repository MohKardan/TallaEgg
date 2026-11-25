using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Core.ErrorHandling;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Verifies that <see cref="WalletRepository.UnlockBalanceAsync"/> and
/// <see cref="WalletService.UnlockBalanceAsync"/> reject non-positive unlock amounts with
/// <see cref="BusinessRuleException"/> (mapped to HTTP 400 by <c>Wallet.Api</c>) rather than
/// throwing an unhandled <see cref="ArgumentOutOfRangeException"/> (HTTP 500) (issue #368).
/// </summary>
public class WalletUnlockBalanceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public WalletUnlockBalanceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private WalletDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options);

    private WalletService NewService(WalletDbContext context) =>
        new(new WalletRepository(NullLogger<WalletRepository>.Instance, context),
            new WalletMapper(),
            NullLogger<WalletService>.Instance);

    [Theory]
    [InlineData(-100)]
    [InlineData(-0.01)]
    [InlineData(0)]
    public async Task UnlockBalanceAsync_WithNonPositiveAmount_ThrowsBusinessRuleExceptionAndLeavesBalancesIntact(decimal badAmount)
    {
        var userId = Guid.NewGuid();
        using (var seed = NewContext())
        {
            var service = NewService(seed);
            await service.DepositAsync(userId, CurrenciesConstant.Toman, 1_000m);
            await service.LockBalanceAsync(userId, CurrenciesConstant.Toman, 400m);
        }

        using (var act = NewContext())
        {
            var service = NewService(act);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.UnlockBalanceAsync(userId, CurrenciesConstant.Toman, badAmount));

            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        }

        using var verify = NewContext();
        var wallet = await verify.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == CurrenciesConstant.Toman);
        Assert.Equal(600m, wallet.Balance);
        Assert.Equal(400m, wallet.LockedBalance);
    }

    [Fact]
    public async Task UnlockBalanceAsync_WithNegativeAmountAndNoExistingWallet_DoesNotCreateWallet()
    {
        var userId = Guid.NewGuid();

        using (var act = NewContext())
        {
            var service = NewService(act);

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.UnlockBalanceAsync(userId, CurrenciesConstant.Toman, -50m));
        }

        using var verify = NewContext();
        Assert.Empty(verify.Wallets.Where(w => w.UserId == userId));
    }

    [Fact]
    public async Task UnlockBalanceAsync_WithValidPositiveAmount_ReleasesLockedFundsToBalance()
    {
        var userId = Guid.NewGuid();
        using (var seed = NewContext())
        {
            var service = NewService(seed);
            await service.DepositAsync(userId, CurrenciesConstant.Toman, 1_000m);
            await service.LockBalanceAsync(userId, CurrenciesConstant.Toman, 400m);
        }

        using (var act = NewContext())
        {
            var service = NewService(act);
            var dto = await service.UnlockBalanceAsync(userId, CurrenciesConstant.Toman, 150m);

            Assert.Equal(750m, dto.Balance);
            Assert.Equal(250m, dto.LockedBalance);
        }
    }
}
