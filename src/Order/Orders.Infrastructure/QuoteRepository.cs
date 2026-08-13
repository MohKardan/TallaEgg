using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Core;

namespace Orders.Infrastructure;

public class QuoteRepository : IQuoteRepository
{
    private readonly OrdersDbContext _context;
    private readonly ILogger<QuoteRepository> _logger;

    public QuoteRepository(OrdersDbContext context, ILogger<QuoteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Quote?> GetActiveAsync(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        // اگر به هر دلیل بیش از یکی فعال بود، تازه‌ترین برنده است. SingleOrDefault اینجا
        // اشتباه بود: کل جریان معامله را با استثنا می‌شکست، در حالی که یک جواب کاملاً
        // معقول وجود دارد.
        return await _context.Quotes
            .Where(q => q.Symbol == normalized && q.IsActive)
            .OrderByDescending(q => q.PublishedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<(IReadOnlyList<Quote> Items, int TotalCount)> GetHistoryAsync(
        string symbol, int pageNumber, int pageSize)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        // Clamped rather than trusted: these arrive from an HTTP query string, and a page
        // size of zero would divide by zero when the caller computes the page count, while a
        // huge one would build a message Telegram refuses to send.
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Quotes.Where(q => q.Symbol == normalized);

        var total = await query.CountAsync();

        // Ordered by PublishedAt, not by Id: ids are Guids and carry no order at all, so
        // paging on them would return the same rows on different pages.
        var items = await query
            .OrderByDescending(q => q.PublishedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IReadOnlyList<string>> GetActiveSymbolsAsync()
    {
        return await _context.Quotes
            .Where(q => q.IsActive)
            .Select(q => q.Symbol)
            .Distinct()
            .ToListAsync();
    }

    public async Task<Quote> PublishAsync(Quote quote)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            // غیرفعال‌سازی قبلی و درج جدید باید با هم commit شوند، وگرنه لحظه‌ای وجود دارد
            // که نماد یا دو مظنهٔ فعال دارد یا هیچ.
            var previous = await _context.Quotes
                .Where(q => q.Symbol == quote.Symbol && q.IsActive)
                .ToListAsync();

            foreach (var old in previous)
                old.Deactivate();

            _context.Quotes.Add(quote);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Published quote {QuoteId} for {Symbol}: buy {BuyPrice}, sell {SellPrice} (replaced {ReplacedCount}).",
                quote.Id, quote.Symbol, quote.BuyPrice, quote.SellPrice, previous.Count);

            return quote;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to publish quote for {Symbol}", quote.Symbol);
            throw;
        }
    }
}
