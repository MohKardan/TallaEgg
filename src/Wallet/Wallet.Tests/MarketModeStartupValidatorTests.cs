using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// The startup check for issue #73: a symbol with an active published quote that isn't
/// configured for Dealer mode is a contradiction — nobody publishes a price for a market that
/// doesn't read prices. <see cref="MarketModeTests.WithNoConfiguration_TheModeIsOrderBook"/>
/// covers the unit-level default and is untouched by this; these tests cover the system-level
/// check built on top of it.
/// </summary>
public class MarketModeStartupValidatorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MarketModeStartupValidatorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private OrdersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    private static MarketModeProvider ModeProvider(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();

        return new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);
    }

    /// <summary>Records every log call so a test can inspect level and message.</summary>
    private sealed class CapturingLogger : ILogger<MarketModeStartupValidator>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    [Fact]
    public async Task ActiveQuoteWithoutDealerMode_LogsAnErrorNamingTheSymbol()
    {
        using var context = NewContext();
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);
        await quoteRepository.PublishAsync(Quote.Publish("MAUA/IRT", 100m, 105m, Guid.NewGuid()));

        // No configuration at all — the exact scenario from the issue: the quote is real, the
        // setting was never written, and nothing before this check ever compared the two.
        var logger = new CapturingLogger();
        var validator = new MarketModeStartupValidator(quoteRepository, ModeProvider(), logger);

        await validator.ValidateAsync();

        var error = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("MAUA/IRT", error.Message);
    }

    [Fact]
    public async Task ActiveQuoteWithDealerMode_LogsNothing()
    {
        using var context = NewContext();
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);
        await quoteRepository.PublishAsync(Quote.Publish("MAUA/IRT", 100m, 105m, Guid.NewGuid()));

        var marketMode = ModeProvider(("Matching:MarketModes:MAUA/IRT", "Dealer"));
        var logger = new CapturingLogger();
        var validator = new MarketModeStartupValidator(quoteRepository, marketMode, logger);

        await validator.ValidateAsync();

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task NoActiveQuotes_LogsNothing()
    {
        using var context = NewContext();
        var quoteRepository = new QuoteRepository(context, NullLogger<QuoteRepository>.Instance);

        var logger = new CapturingLogger();
        var validator = new MarketModeStartupValidator(quoteRepository, ModeProvider(), logger);

        await validator.ValidateAsync();

        Assert.Empty(logger.Entries);
    }
}
