using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// مظنهٔ ادمین: قیمتی که منتشر می‌شود، بدون اینکه سفارشی در دفتر بگذارد یا وثیقه‌ای
/// قفل کند (issue #48).
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

    // ── قیمت‌گذاری ──────────────────────────────────────────────────────────────

    /// <summary>
    /// مشتری از ادمین می‌خرد، پس قیمتِ <b>فروشِ</b> ادمین را می‌پردازد. جابه‌جا کردن این
    /// دو، همان باگ وارونگی خریدار/فروشنده است با لباس دیگر.
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
    /// اسپرد منفی یعنی ادمین گران‌تر می‌خرد از آنچه می‌فروشد. مشتری می‌توانست بی‌نهایت بار
    /// بخرد و بفروشد و هر بار سود کند — مستقیماً از جیب طلافروش.
    /// </summary>
    [Fact]
    public void ANegativeSpreadIsRejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Quote.Publish(Symbol, buyPrice: 18_000_000m, sellPrice: 17_000_000m, Admin));

        Assert.Contains("قیمت خرید", ex.Message);
    }

    /// <summary>اسپرد صفر مجاز است — ادمین بدون حاشیه معامله می‌کند، ولی ضرر نمی‌کند.</summary>
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
        Assert.Throws<ArgumentException>(() => Quote.Publish(Symbol, badPrice, 17_500_000m, Admin));
        Assert.Throws<ArgumentException>(() => Quote.Publish(Symbol, 17_000_000m, badPrice, Admin));
    }

    // ── انتشار ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishingMakesTheQuoteReadable()
    {
        await _repository.PublishAsync(Quote.Publish(Symbol, 17_000_000m, 17_500_000m, Admin));

        var active = await _repository.GetActiveAsync(Symbol);

        Assert.NotNull(active);
        Assert.Equal(17_500_000m, active!.SellPrice);
    }

    /// <summary>
    /// انتشار مظنهٔ جدید باید قبلی را کنار بگذارد. اگر دو مظنه فعال بمانند، معلوم نیست
    /// مشتری روی کدام قیمت معامله می‌کند.
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
    /// مظنهٔ قدیمی حذف نمی‌شود؛ فقط غیرفعال می‌شود — تا بعداً بشود فهمید هر معامله روی چه
    /// قیمتی انجام شده.
    /// </summary>
    [Fact]
    public async Task ThePreviousQuoteIsKeptAsHistory()
    {
        await _repository.PublishAsync(Quote.Publish(Symbol, 17_000_000m, 17_500_000m, Admin));
        await _repository.PublishAsync(Quote.Publish(Symbol, 18_000_000m, 18_500_000m, Admin));

        Assert.Equal(2, await _context.Quotes.CountAsync());
        Assert.Equal(1, await _context.Quotes.CountAsync(q => !q.IsActive && q.DeactivatedAt != null));
    }

    /// <summary>نمادها بر هم اثر نمی‌گذارند: انتشار برای یکی، مظنهٔ دیگری را کنار نمی‌زند.</summary>
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

    /// <summary>نماد باید بدون توجه به بزرگی و کوچکی حروف پیدا شود.</summary>
    [Fact]
    public async Task SymbolLookupIsCaseInsensitive()
    {
        await _repository.PublishAsync(Quote.Publish("maua/irt", 17_000_000m, 17_500_000m, Admin));

        Assert.NotNull(await _repository.GetActiveAsync("MAUA/IRT"));
    }
}
