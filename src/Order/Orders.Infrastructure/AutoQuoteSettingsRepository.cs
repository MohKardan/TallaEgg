using Microsoft.EntityFrameworkCore;
using Orders.Core;

namespace Orders.Infrastructure;

public class AutoQuoteSettingsRepository : IAutoQuoteSettingsRepository
{
    private readonly OrdersDbContext _context;

    public AutoQuoteSettingsRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<AutoQuoteSettings> GetOrCreateAsync(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        var existing = await _context.AutoQuoteSettings
            .FirstOrDefaultAsync(s => s.Symbol == normalized);

        if (existing is not null)
            return existing;

        var created = AutoQuoteSettings.CreateDefault(normalized);
        _context.AutoQuoteSettings.Add(created);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two requests racing to create the first row for this symbol — the unique index
            // on Symbol rejects the loser. Whoever lost reads what the winner actually wrote,
            // rather than surfacing a database error for what is, from the caller's point of
            // view, a successful "get or create".
            _context.Entry(created).State = EntityState.Detached;
            return await _context.AutoQuoteSettings.SingleAsync(s => s.Symbol == normalized);
        }

        return created;
    }

    public Task SaveAsync(AutoQuoteSettings settings) => _context.SaveChangesAsync();
}
