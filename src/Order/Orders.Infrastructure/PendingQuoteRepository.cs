using Microsoft.EntityFrameworkCore;
using TallaEgg.Core.ErrorHandling;
using Microsoft.Extensions.Logging;
using Orders.Core;

namespace Orders.Infrastructure;

public class PendingQuoteRepository : IPendingQuoteRepository
{
    private readonly OrdersDbContext _context;
    private readonly ILogger<PendingQuoteRepository> _logger;

    public PendingQuoteRepository(OrdersDbContext context, ILogger<PendingQuoteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PendingQuote> ProposeAsync(PendingQuote pendingQuote)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            // Superseding the previous proposal and inserting the new one must commit together, or
            // there is an instant where the symbol has either two live proposals or none — and the
            // bot polls often enough to see it.
            var live = await _context.PendingQuotes
                .Where(p => p.Symbol == pendingQuote.Symbol && p.Status == PendingQuoteStatus.Pending)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var previous in live)
                previous.Supersede(now);

            _context.PendingQuotes.Add(pendingQuote);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Quote for {Symbol} held for approval: buy {BuyPrice}, sell {SellPrice}, {Deviation}% from the last mid " +
                "(superseded {SupersededCount} earlier proposal(s)).",
                pendingQuote.Symbol, pendingQuote.BuyPrice, pendingQuote.SellPrice,
                decimal.Round(pendingQuote.DeviationPercent, 4), live.Count);

            return pendingQuote;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to hold a quote for approval for {Symbol}", pendingQuote.Symbol);
            throw;
        }
    }

    public async Task<IReadOnlyList<PendingQuote>> GetAwaitingApprovalAsync()
    {
        var cutoff = DateTime.UtcNow - PendingQuote.Lifetime;

        // Filtered by age as well as status: ExpireStaleAsync is what actually closes these rows,
        // and it runs on the same schedule as the poll, so without this a proposal could be offered
        // to an admin in the moment between passing its deadline and being marked expired.
        return await _context.PendingQuotes
            .Where(p => p.Status == PendingQuoteStatus.Pending && p.CreatedAt > cutoff)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<PendingQuote?> GetAsync(Guid id) =>
        await _context.PendingQuotes.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Quote> ApproveAsync(PendingQuote pendingQuote, Guid approvedByUserId)
    {
        // Approve() refuses anything but a live, unexpired proposal, so this throws before any
        // write when an admin taps a button on a message that has been sitting in Telegram.
        var quote = pendingQuote.Approve(approvedByUserId, DateTime.UtcNow);

        await using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            // The same two writes IQuoteRepository.PublishAsync performs, inline rather than
            // delegated, because they have to share this transaction with the approval: neither
            // may happen without the other, and PublishAsync opens a transaction of its own,
            // which cannot nest. Six duplicated lines with the reason written down beat an
            // abstraction that would have to thread a transaction through both callers.
            var previous = await _context.Quotes
                .Where(q => q.Symbol == quote.Symbol && q.IsActive)
                .ToListAsync();

            foreach (var old in previous)
                old.Deactivate();

            _context.Quotes.Add(quote);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Quote for {Symbol} approved by {ApprovedBy} and published: buy {BuyPrice}, sell {SellPrice} " +
                "({Deviation}% from the mid it was measured against).",
                quote.Symbol, approvedByUserId, quote.BuyPrice, quote.SellPrice,
                decimal.Round(pendingQuote.DeviationPercent, 4));

            return quote;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another admin answered between this one's read and this write. The token matched
            // zero rows, so nothing was published and nothing is rolled forward — reported as the
            // ordinary "already answered" it is, rather than as a fault.
            await tx.RollbackAsync();

            _logger.LogInformation(
                "Quote for {Symbol} was answered concurrently by another admin; this approval published nothing.",
                pendingQuote.Symbol);

            throw new BusinessRuleException("این مظنه پیش‌تر بررسی شده است.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to publish an approved quote for {Symbol}", pendingQuote.Symbol);
            throw;
        }
    }

    public async Task RejectAsync(PendingQuote pendingQuote, Guid rejectedByUserId)
    {
        pendingQuote.Reject(rejectedByUserId, DateTime.UtcNow);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Same race as approval, and the same answer: somebody got here first.
            throw new BusinessRuleException("این مظنه پیش‌تر بررسی شده است.");
        }

        _logger.LogInformation(
            "Quote for {Symbol} rejected by {RejectedBy}; the previous quote stands.",
            pendingQuote.Symbol, rejectedByUserId);
    }

    public async Task<int> ExpireStaleAsync()
    {
        var now = DateTime.UtcNow;
        var cutoff = now - PendingQuote.Lifetime;

        var stale = await _context.PendingQuotes
            .Where(p => p.Status == PendingQuoteStatus.Pending && p.CreatedAt <= cutoff)
            .ToListAsync();

        if (stale.Count == 0) return 0;

        foreach (var pendingQuote in stale)
            pendingQuote.Expire(now);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "{Count} quote proposal(s) expired unanswered after {Minutes} minutes.",
            stale.Count, PendingQuote.Lifetime.TotalMinutes);

        return stale.Count;
    }
}
