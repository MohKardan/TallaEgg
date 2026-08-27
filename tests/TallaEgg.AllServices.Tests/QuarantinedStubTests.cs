using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What sits behind the <c>POST /api/wallet/transaction/trade</c> quarantine (audit finding C-8,
/// issue #46).
///
/// These replace <c>Wallet.Api/Tests/QuarantinedEndpointsTests.cs</c>, which was deleted. That file
/// had two problems: it did not compile at all — Wallet.Api has neither NUnit nor Moq, and
/// <c>Results.StatusCode</c> has no two-argument overload — and, more importantly, its <c>Setup</c>
/// built a fresh <c>WebApplication</c> and <b>redefined the endpoint</b>, so it asserted against a
/// copy declared inside the test file rather than what <c>Program.cs</c> actually runs. Deleting the
/// real endpoint would have left those tests green.
///
/// Why the risk is real: turn <c>FeatureFlags:QuarantineStubEndpoints</c> off and the endpoint
/// reaches <see cref="WalletService.MakeTradeAsync"/> — which returns
/// <c>new WalletBallanceDTO()</c>, meaning <b>HTTP 200 with zeroes</b> and not a single unit of
/// currency moved. To the caller that is worse than a 501: a silent failure instead of an explicit
/// refusal.
///
/// These tests pin that. If <c>MakeTradeAsync</c> is ever genuinely implemented this file breaks —
/// and it should: the same change must lift the quarantine and replace these tests with real
/// transfer tests.
/// </summary>
public class QuarantinedStubTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WalletDbContext _context;
    private readonly WalletService _service;

    private readonly Guid _fromUserId = Guid.NewGuid();
    private readonly Guid _toUserId = Guid.NewGuid();
    private const string Asset = "MAUA";

    public QuarantinedStubTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;
        _context = new WalletDbContext(options);
        _context.Database.EnsureCreated();

        var repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);
        _service = new WalletService(repository, new WalletMapper());

        Seed(_fromUserId, balance: 100m);
        Seed(_toUserId, balance: 0m);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void Seed(Guid userId, decimal balance)
    {
        var wallet = WalletEntity.Create(userId, Asset);
        wallet.Balance = balance;
        _context.Wallets.Add(wallet);
    }

    private async Task<decimal> BalanceOfAsync(Guid userId)
    {
        _context.ChangeTracker.Clear();
        return (await _context.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == Asset)).Balance;
    }

    /// <summary>
    /// The central claim: the method returns successfully while moving no money. That is precisely
    /// what makes the quarantine necessary — a method that threw would be safer.
    /// </summary>
    [Fact]
    public async Task MakeTradeAsync_ReportsSuccessButMovesNoMoney()
    {
        var result = await _service.MakeTradeAsync(_fromUserId, _toUserId, Asset, amount: 25m, referenceId: "REF-1");

        Assert.NotNull(result); // بدون استثنا، بدون پیام خطا — یعنی «انجام شد»

        Assert.Equal(100m, await BalanceOfAsync(_fromUserId)); // چیزی کسر نشد
        Assert.Equal(0m, await BalanceOfAsync(_toUserId));     // چیزی اضافه نشد
        Assert.Empty(_context.Transactions);                   // و هیچ ردی ثبت نشد
    }

    /// <summary>
    /// The result is not merely wrong but <b>empty</b>: any caller reading the before and after
    /// balances sees zeroes, and there is no tracking code either. Nothing tells the caller that
    /// nothing happened.
    /// </summary>
    [Fact]
    public async Task MakeTradeAsync_ReturnsAnEmptyResultWithNoTrackingCode()
    {
        var result = await _service.MakeTradeAsync(_fromUserId, _toUserId, Asset, amount: 25m, referenceId: "REF-2");

        Assert.Equal(0m, result.BalanceBefore);
        Assert.Equal(0m, result.BalanceAfter);
        Assert.True(string.IsNullOrEmpty(result.TrackingCode));
    }

    /// <summary>
    /// The two guards that do work must stay — they are this method's only validation, and the only
    /// thing a future implementation could quietly lose.
    /// </summary>
    [Fact]
    public async Task MakeTradeAsync_StillRefusesATransferToSelf()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MakeTradeAsync(_fromUserId, _fromUserId, Asset, 25m, "REF-3"));
    }

    [Fact]
    public async Task MakeTradeAsync_StillRefusesAMissingWallet()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MakeTradeAsync(_fromUserId, Guid.NewGuid(), Asset, 25m, "REF-4"));
    }
}
