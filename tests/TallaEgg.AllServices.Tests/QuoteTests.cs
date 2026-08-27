using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.ErrorHandling;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The admin's quote: a published price that places no order in the book and locks no collateral
/// (issue #48).
/// </summary>
public class QuoteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly OrdersDbContext _context;
    private readonly QuoteRepository _repository;

    private const string Symbol = "MAUA/IRT";
    private static readonly Guid Admin = Guid.NewGuid();

    public QuoteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;
        _context = new OrdersDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new QuoteRepository(_context, NullLogger<QuoteRepository>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // ── Pricing ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A customer buys from the admin, so they pay the admin's <b>sell</b> price. Swapping the two is
    /// the buyer/seller inversion bug wearing a different hat.
    /// </summary>
    [Fact]
    public void CustomerBuyingPaysTheAdminsSellPrice()
    {
        var quote = Quote.Publish(Symbol, buyPrice: 17_000_000m, sellPrice: 17_500_000m, Admin);

        Assert.Equal(17_500_000m, quote.PriceFor(OrderSide.Buy));
    }

    [Fact]
    public void CustomerSellingReceivesTheAdminsBuyPrice()
    {
        var quote = Quote.Publish(Symbol, buyPrice: 17_000_000m, sellPrice: 17_500_000m, Admin);

        Assert.Equal(17_000_000m, quote.PriceFor(OrderSide.Sell));
    }

    /// <summary>
    /// A negative spread means the admin buys higher than they sell. A customer could buy and sell
    /// endlessly, profiting every time, straight out of the shop's pocket.
    /// </summary>
    [Fact]
    public void ANegativeSpreadIsRejected()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            Quote.Publish(Symbol, buyPrice: 18_000_000m, sellPrice: 17_000_000m, Admin));

        Assert.Contains("قیمت خرید", ex.Message);
    }

    /// <summary>A zero spread is allowed: the admin trades at no margin, but does not lose.</summary>
    [Fact]
    public void AZeroSpreadIsAllowed()
    {
        var quote = Quote.Publish(Symbol, buyPrice: 17_000_000m, sellPrice: 17_000_000m, Admin);

        Assert.Equal(quote.BuyPrice, quote.SellPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePricesAreRejected(decimal badPrice)
    {
        Assert.Throws<BusinessRuleException>(() => Quote.Publish(Symbol, badPrice, 17_500_000m, Admin));
        Assert.Throws<BusinessRuleException>(() => Quote.Publish(Symbol, 17_000_000m, badPrice, Admin));
    }

    // ── Publishing ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishingMakesTheQuoteReadable()
    {
        await _repository.PublishAsync(Quote.Publish(Symbol, 17_000_000m, 17_500_000m, Admin));

        var active = await _repository.GetActiveAsync(Symbol);

        Assert.NotNull(active);
        Assert.Equal(17_500_000m, active!.SellPrice);
    }

    /// <summary>
    /// Publishing a new quote must retire the previous one. With two active quotes it is undefined
    /// Which price the customer trades at.
    /// </summary>
    [Fact]
    public async Task PublishingAgainReplacesThePreviousQuote()
    {
        await _repository.PublishAsync(Quote.Publish(Symbol, 17_000_000m, 17_500_000m, Admin));
        await _repository.PublishAsync(Quote.Publish(Symbol, 18_000_000m, 18_500_000m, Admin));

        Assert.Equal(1, await _context.Quotes.CountAsync(q => q.Symbol == Symbol && q.IsActive));

        var active = await _repository.GetActiveAsync(Symbol);
        Assert.Equal(18_500_000m, active!.SellPrice);
    }

    /// <summary>
    /// An old quote is deactivated rather than deleted, so it stays possible to find out what price
    /// a past trade used.
    /// </summary>
    [Fact]
    public async Task ThePreviousQuoteIsKeptAsHistory()
    {
        await _repository.PublishAsync(Quote.Publish(Symbol, 17_000_000m, 17_500_000m, Admin));
        await _repository.PublishAsync(Quote.Publish(Symbol, 18_000_000m, 18_500_000m, Admin));

        Assert.Equal(2, await _context.Quotes.CountAsync());
        Assert.Equal(1, await _context.Quotes.CountAsync(q => !q.IsActive && q.DeactivatedAt != null));
    }

    /// <summary>Symbols do not affect each other: publishing for one does not displace another's quote.</summary>
    [Fact]
    public async Task PublishingForOneSymbolLeavesAnotherAlone()
    {
        await _repository.PublishAsync(Quote.Publish("MAUA/IRT", 17_000_000m, 17_500_000m, Admin));
        await _repository.PublishAsync(Quote.Publish("BTC/IRT", 100m, 110m, Admin));

        Assert.NotNull(await _repository.GetActiveAsync("MAUA/IRT"));
        Assert.NotNull(await _repository.GetActiveAsync("BTC/IRT"));
    }

    [Fact]
    public async Task NoQuotePublishedYetReturnsNull()
    {
        Assert.Null(await _repository.GetActiveAsync(Symbol));
    }

    /// <summary>A symbol must be found regardless of case.</summary>
    [Fact]
    public async Task SymbolLookupIsCaseInsensitive()
    {
        await _repository.PublishAsync(Quote.Publish("maua/irt", 17_000_000m, 17_500_000m, Admin));

        Assert.NotNull(await _repository.GetActiveAsync("MAUA/IRT"));
    }
}
