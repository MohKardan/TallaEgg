using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orders.Core;
using Orders.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// <see cref="SymbolSettingsRepository"/> against a real <see cref="OrdersDbContext"/> (SQLite
/// in-memory) — the same pattern used elsewhere in this project for repository-level tests
/// (<c>QuoteFillCounterpartyTests</c>, <c>MarketModeStartupValidatorTests</c>).
/// </summary>
public class SymbolSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SymbolSettingsRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task GetOrCreateAsync_CreatesAnInactiveRowTheFirstTime()
    {
        using var context = NewContext();
        var repo = new SymbolSettingsRepository(context);

        var settings = await repo.GetOrCreateAsync("SEKE_BAHAR/IRT");

        Assert.False(settings.IsActive);
        Assert.Equal("SEKE_BAHAR/IRT", settings.Symbol);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsTheSameRowOnASecondCall()
    {
        using var context = NewContext();
        var repo = new SymbolSettingsRepository(context);

        var first = await repo.GetOrCreateAsync("BTC/IRT");
        first.SetActive(true, Guid.NewGuid());
        await repo.SaveAsync(first);

        var second = await repo.GetOrCreateAsync("BTC/IRT");

        Assert.Equal(first.Id, second.Id);
        Assert.True(second.IsActive);
    }

    [Fact]
    public async Task GetActiveSymbolsAsync_OnlyReturnsSymbolsTurnedOn()
    {
        using var context = NewContext();
        var repo = new SymbolSettingsRepository(context);

        var maua = await repo.GetOrCreateAsync("MAUA/IRT");
        maua.SetActive(true, Guid.NewGuid());
        await repo.SaveAsync(maua);

        var seke = await repo.GetOrCreateAsync("SEKE_BAHAR/IRT");
        seke.SetActive(false, Guid.NewGuid());
        await repo.SaveAsync(seke);

        var active = await repo.GetActiveSymbolsAsync();

        Assert.Contains("MAUA/IRT", active);
        Assert.DoesNotContain("SEKE_BAHAR/IRT", active);
    }

    [Fact]
    public async Task GetActiveSymbolsAsync_EmptyWhenNothingHasBeenActivated()
    {
        using var context = NewContext();
        var repo = new SymbolSettingsRepository(context);

        Assert.Empty(await repo.GetActiveSymbolsAsync());
    }
}
