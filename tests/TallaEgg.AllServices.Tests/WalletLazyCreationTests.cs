using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A user's wallet for a given asset previously had to already exist — CreateDefaultWalletsAsync
/// only ever seeds Toman, MAUA, and CREDIT_MAUA at registration, so a deposit or withdrawal in
/// any other asset (a newer trading symbol, or its own CREDIT_ ledger) failed with "کیف پول وجود
/// ندارد" for every user, new or old. First hit live: crediting CREDIT_BTC through the admin
/// «ش» command after #111/#112 added Bitcoin and the coin as tradable symbols.
///
/// A wallet is now created the first time it's actually needed, the same way a customer's
/// MAUA/IRT wallet already was at registration — just lazily instead of up front. A genuinely
/// unknown asset (a typo, not a real symbol) still fails instead of silently creating a phantom
/// wallet.
/// </summary>
public class WalletLazyCreationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public WalletLazyCreationTests()
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
        new(new WalletRepository(NullLogger<WalletRepository>.Instance, context), new WalletMapper());

    [Fact]
    public async Task DepositingAKnownAssetWithNoExistingWallet_CreatesOneRatherThanFailing()
    {
        using var context = NewContext();
        var service = NewService(context);
        var userId = Guid.NewGuid();

        var result = await service.DepositAsync(userId, CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Btc), 100m);

        Assert.Equal(0m, result.BalanceBefore);
        Assert.Equal(100m, result.BalanceAfter);
    }

    [Fact]
    public async Task ASecondDepositToTheSameLazilyCreatedWallet_AddsToItRatherThanRecreatingIt()
    {
        using var context = NewContext();
        var service = NewService(context);
        var userId = Guid.NewGuid();
        var asset = CurrenciesConstant.CreditAssetFor(CurrenciesConstant.SekeBahar);

        await service.DepositAsync(userId, asset, 30m);
        var second = await service.DepositAsync(userId, asset, 20m);

        Assert.Equal(30m, second.BalanceBefore);
        Assert.Equal(50m, second.BalanceAfter);

        using var verify = NewContext();
        Assert.Single(verify.Wallets.Where(w => w.UserId == userId && w.Asset == asset));
    }

    /// <summary>
    /// Regular (non-CREDIT_) assets still refuse to go negative — WalletEntity.DecreaseBalance's
    /// own guard, unrelated to and unchanged by this fix. What this proves is narrower: a
    /// withdrawal against a real asset with no wallet yet now fails for the right reason
    /// (insufficient balance) instead of the wrong one ("کیف پول وجود ندارد", as if the asset
    /// itself were unrecognised) — so the wallet genuinely was created before the balance check
    /// ran, not skipped.
    /// </summary>
    [Fact]
    public async Task WithdrawingFromAKnownAssetWithNoExistingWallet_FailsOnInsufficientBalanceNotMissingWallet()
    {
        using var context = NewContext();
        var service = NewService(context);
        var userId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.WithdrawalAsync(userId, CurrenciesConstant.Btc, 1m));

        Assert.DoesNotContain("وجود ندارد", ex.Message);
    }

    /// <summary>
    /// Depositing first (the realistic شارژ flow) then withdrawing within that balance succeeds
    /// cleanly through the same lazily-created wallet — the ordinary, non-edge-case path.
    /// </summary>
    [Fact]
    public async Task DepositingThenWithdrawingWithinBalance_UsesTheSameLazilyCreatedWallet()
    {
        using var context = NewContext();
        var service = NewService(context);
        var userId = Guid.NewGuid();
        var asset = CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Btc);

        await service.DepositAsync(userId, asset, 5m);
        var result = await service.WithdrawalAsync(userId, asset, 2m);

        Assert.Equal(5m, result.BalanceBefore);
        Assert.Equal(3m, result.BalanceAfter);
    }

    [Fact]
    public async Task DepositingAnUnrecognisedAsset_StillFailsInsteadOfCreatingAPhantomWallet()
    {
        using var context = NewContext();
        var service = NewService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.DepositAsync(Guid.NewGuid(), "NOT_A_REAL_ASSET", 100m));
    }
}
