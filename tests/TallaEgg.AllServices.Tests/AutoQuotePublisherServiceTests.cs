using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// One tick of the auto-quote publisher (issue #90): whether it publishes at all, whether what
/// it publishes is arithmetically what an admin would have typed by hand, and — since the coin
/// and Bitcoin symbols were added — whether each symbol is published independently of the
/// others.
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
        services.AddScoped(_ => new ReferencePriceProviderChain([_stubProvider], NullLogger<ReferencePriceProviderChain>.Instance));
        _provider = services.BuildServiceProvider();
    }

    private DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// The one reference price source the test container knows about; mutable per test.
    /// Returns the same price regardless of symbol asked about, since these tests each
    /// exercise one symbol's tick logic at a time — <see cref="ReferencePriceProviderChainTests"/>
    /// covers per-symbol routing at the chain level.
    /// </summary>
    private sealed class StubProvider : IReferencePriceProvider
    {
        public string Name => "stub";
        public decimal? Price { get; set; }
        public Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(Price);
    }

    private async Task RunTickAsync(string symbol = Symbol)
    {
        var service = new AutoQuotePublisherService(
            _provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AutoQuotePublisherService>.Instance);

        await service.PublishIfDueAsync(symbol, CancellationToken.None);
    }

    private async Task SeedSettingsAsync(bool isEnabled, decimal spreadPercent, Guid updatedBy, string symbol = Symbol)
    {
        using var db = new OrdersDbContext(Options());
        var settings = AutoQuoteSettings.CreateDefault(symbol);
        settings.UpdateSpread(spreadPercent, updatedBy);
        settings.SetEnabled(isEnabled, updatedBy);
        db.AutoQuoteSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    private async Task<Quote?> ActiveQuoteAsync(string symbol = Symbol)
    {
        using var db = new OrdersDbContext(Options());
        return await db.Quotes.Where(q => q.Symbol == symbol && q.IsActive).SingleOrDefaultAsync();
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
    /// The provider now returns Toman per traded unit directly — mesghal-to-gram conversion
    /// moved into the gold-specific providers (<c>NerkhPriceProvider</c>,
    /// <c>BrsApiPriceProvider</c>) since it doesn't apply to the coin or Bitcoin symbols. So the
    /// publisher's own math is just the spread, applied to whatever the chain returned: 1% of
    /// 1,000,000 is 10,000 either side.
    /// </summary>
    [Fact]
    public async Task PublishesTheSpreadAroundTheReferencePrice()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        _stubProvider.Price = 1_000_000m;

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
        _stubProvider.Price = 1_000_000m;

        await RunTickAsync();

        var quote = await ActiveQuoteAsync();
        Assert.Equal(admin, quote!.PublishedByUserId);
    }

    [Fact]
    public async Task ASecondTickReplacesTheFirstQuoteRatherThanAddingASecondActiveOne()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());

        _stubProvider.Price = 1_000_000m;
        await RunTickAsync();

        _stubProvider.Price = 1_050_000m;
        await RunTickAsync();

        using var db = new OrdersDbContext(Options());
        Assert.Single(db.Quotes.Where(q => q.Symbol == Symbol && q.IsActive));
    }

    /// <summary>
    /// Added alongside the coin and Bitcoin symbols: each symbol's <c>AutoQuoteSettings</c> row
    /// is independent, so one being enabled and another disabled must not cross-contaminate —
    /// the publisher loop in <c>ExecuteAsync</c> calls this per symbol, but the per-symbol
    /// isolation is what <see cref="PublishIfDueAsync"/> itself is responsible for.
    /// </summary>
    [Fact]
    public async Task EachSymbolPublishesIndependently()
    {
        var coin = CurrenciesConstant.SEKE_BAHAR_IRT;

        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid(), symbol: Symbol);
        await SeedSettingsAsync(isEnabled: false, spreadPercent: 1m, Guid.NewGuid(), symbol: coin);

        _stubProvider.Price = 1_000_000m;
        await RunTickAsync(Symbol);
        await RunTickAsync(coin);

        Assert.NotNull(await ActiveQuoteAsync(Symbol));
        Assert.Null(await ActiveQuoteAsync(coin));
    }
}
