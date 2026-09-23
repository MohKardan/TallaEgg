using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A tick publishes its symbols together, not one after another (issue #317).
///
/// <para>
/// Sequentially, a tick lasted as long as the sum of its symbols, and each symbol may try six
/// price sources over the network. With four symbols and a host that accepts a connection and then
/// goes quiet, one tick could outlast the six-minute lease that stops a second instance publishing
/// the same prices — and that lease is renewed <b>between</b> ticks, never during one (#160). A
/// tick now lasts as long as its slowest symbol, which stays true as symbols are added.
/// </para>
/// </summary>
public class AutoQuoteTickConcurrencyTests : IDisposable
{
    private static readonly string[] Symbols = [CurrenciesConstant.MAUA_IRT, CurrenciesConstant.SEKE_BAHAR_IRT];

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly RendezvousProvider _prices = new(expected: Symbols.Length);

    public AutoQuoteTickConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var setup = new OrdersDbContext(Options()))
            setup.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped(_ => new OrdersDbContext(Options()));
        services.AddScoped<IAutoQuoteSettingsRepository, AutoQuoteSettingsRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IPendingQuoteRepository, PendingQuoteRepository>();
        services.AddScoped(_ => new ReferencePriceProviderChain(
            [_prices], NullLogger<ReferencePriceProviderChain>.Instance,
            new ConfigurationBuilder().Build(), TimeProvider.System));

        _provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Answers only once every symbol of the tick has asked. Run sequentially, the first symbol
    /// waits for a second that cannot start, so the tick never finishes — the test fails by
    /// timeout rather than passing slowly, which is what makes it a test of concurrency rather
    /// than of speed.
    /// </summary>
    private sealed class RendezvousProvider(int expected) : IReferencePriceProvider
    {
        private readonly TaskCompletionSource _allArrived = new();
        private int _arrived;

        public string Name => "rendezvous";

        public async Task<ReferencePrice?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrived) == expected) _allArrived.SetResult();

            await _allArrived.Task.WaitAsync(cancellationToken);
            return new ReferencePrice(80_000_000m, null);
        }
    }

    [Fact]
    public async Task OneTick_PublishesItsSymbolsTogether()
    {
        foreach (var symbol in Symbols)
            await SeedEnabledAsync(symbol);

        var service = new AutoQuotePublisherService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoQuotePublisherService>.Instance,
            new AlwaysLeaderLease(),
            MigratedDatabase.Readiness());

        await service.PublishAllAsync(Symbols, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        using var db = new OrdersDbContext(Options());
        foreach (var symbol in Symbols)
            Assert.True(await db.Quotes.AnyAsync(q => q.Symbol == symbol && q.IsActive), $"{symbol} was not published.");
    }

    /// <summary>
    /// One symbol failing must not take the rest of the tick with it. Under
    /// <c>Task.WhenAll</c> that matters more than it did in a loop: an exception escaping one
    /// symbol's task would abandon the others.
    /// </summary>
    [Fact]
    public async Task OneSymbolThrowing_DoesNotStopTheOthers()
    {
        foreach (var symbol in Symbols)
            await SeedEnabledAsync(symbol);

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped(_ => new OrdersDbContext(Options()));
        services.AddScoped<IAutoQuoteSettingsRepository, AutoQuoteSettingsRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IPendingQuoteRepository, PendingQuoteRepository>();
        services.AddScoped(_ => new ReferencePriceProviderChain(
            [new ThrowsForOneSymbolProvider(CurrenciesConstant.MAUA_IRT)],
            NullLogger<ReferencePriceProviderChain>.Instance,
            new ConfigurationBuilder().Build(), TimeProvider.System));

        using var provider = services.BuildServiceProvider();

        var service = new AutoQuotePublisherService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoQuotePublisherService>.Instance,
            new AlwaysLeaderLease(),
            MigratedDatabase.Readiness());

        await service.PublishAllAsync(Symbols, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        using var db = new OrdersDbContext(Options());
        Assert.False(await db.Quotes.AnyAsync(q => q.Symbol == CurrenciesConstant.MAUA_IRT && q.IsActive));
        Assert.True(await db.Quotes.AnyAsync(q => q.Symbol == CurrenciesConstant.SEKE_BAHAR_IRT && q.IsActive));
    }

    private sealed class ThrowsForOneSymbolProvider(string failing) : IReferencePriceProvider
    {
        public string Name => "throws";

        public Task<ReferencePrice?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default) =>
            symbol == failing
                ? throw new InvalidOperationException("this source is broken for this symbol")
                : Task.FromResult<ReferencePrice?>(new ReferencePrice(230_000_000m, null));
    }

    private async Task SeedEnabledAsync(string symbol)
    {
        using var db = new OrdersDbContext(Options());
        var settings = AutoQuoteSettings.CreateDefault(symbol);
        settings.UpdateSpread(0.5m, Guid.NewGuid());
        settings.SetEnabled(true, Guid.NewGuid());
        db.AutoQuoteSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    private DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
