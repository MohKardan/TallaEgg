using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;

namespace Wallet.Tests;

/// <summary>
/// One tick of the auto-quote publisher (issue #90): whether it publishes at all, and whether
/// what it publishes is arithmetically what an admin would have typed by hand.
///
/// Runs against a real <see cref="OrdersDbContext"/> (SQLite in-memory), the same pattern
/// <c>OutboxDueSelectionTests</c> uses for the other <c>BackgroundService</c> in this project —
/// the interesting bugs here are in what actually gets read from and written to the database,
/// not in isolated arithmetic.
/// </summary>
public class AutoQuotePublisherServiceTests : IDisposable
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly StubProvider _stubProvider = new();

    public AutoQuotePublisherServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var setup = new OrdersDbContext(Options()))
            setup.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddScoped(_ => new OrdersDbContext(Options()));
        services.AddScoped<IAutoQuoteSettingsRepository, AutoQuoteSettingsRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped(_ => new GoldPriceProviderChain([_stubProvider], NullLogger<GoldPriceProviderChain>.Instance));
        _provider = services.BuildServiceProvider();
    }

    private DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>The one gold price source the test container knows about; mutable per test.</summary>
    private sealed class StubProvider : IGoldPriceProvider
    {
        public string Name => "stub";
        public decimal? Price { get; set; }
        public Task<decimal?> GetMesghalPriceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Price);
    }

    private async Task RunTickAsync()
    {
        var service = new AutoQuotePublisherService(
            _provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AutoQuotePublisherService>.Instance);

        await service.PublishIfDueAsync(CancellationToken.None);
    }

    private async Task SeedSettingsAsync(bool isEnabled, decimal spreadPercent, Guid updatedBy)
    {
        using var db = new OrdersDbContext(Options());
        var settings = AutoQuoteSettings.CreateDefault(Symbol);
        settings.UpdateSpread(spreadPercent, updatedBy);
        settings.SetEnabled(isEnabled, updatedBy);
        db.AutoQuoteSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    private async Task<Quote?> ActiveQuoteAsync()
    {
        using var db = new OrdersDbContext(Options());
        return await db.Quotes.Where(q => q.Symbol == Symbol && q.IsActive).SingleOrDefaultAsync();
    }

    [Fact]
    public async Task PublishesNothing_WhenDisabled()
    {
        await SeedSettingsAsync(isEnabled: false, spreadPercent: 1m, Guid.NewGuid());
        _stubProvider.Price = 80_000_000m;

        await RunTickAsync();

        Assert.Null(await ActiveQuoteAsync());
    }

    [Fact]
    public async Task PublishesNothing_WhenNoProviderAnswers()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        _stubProvider.Price = null;

        await RunTickAsync();

        Assert.Null(await ActiveQuoteAsync());
    }

    /// <summary>
    /// 4,331,800 Toman/mesghal ÷ 4.3318 g/mesghal is exactly 1,000,000 Toman/gram — chosen so
    /// the spread math below has no rounding to reason about.
    /// </summary>
    [Fact]
    public async Task PublishesTheSpreadAroundTheConvertedPerGramPrice()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        _stubProvider.Price = 4_331_800m;

        await RunTickAsync();

        var quote = await ActiveQuoteAsync();
        Assert.NotNull(quote);
        Assert.Equal(995_000m, quote!.BuyPrice);
        Assert.Equal(1_005_000m, quote.SellPrice);
    }

    [Fact]
    public async Task ThePublishedQuoteBelongsToWhoeverLastConfiguredAutoQuote()
    {
        var admin = Guid.NewGuid();
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 0.5m, admin);
        _stubProvider.Price = 4_331_800m;

        await RunTickAsync();

        var quote = await ActiveQuoteAsync();
        Assert.Equal(admin, quote!.PublishedByUserId);
    }

    [Fact]
    public async Task ASecondTickReplacesTheFirstQuoteRatherThanAddingASecondActiveOne()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());

        _stubProvider.Price = 4_331_800m;
        await RunTickAsync();

        _stubProvider.Price = 4_500_000m;
        await RunTickAsync();

        using var db = new OrdersDbContext(Options());
        Assert.Single(db.Quotes.Where(q => q.Symbol == Symbol && q.IsActive));
    }
}
