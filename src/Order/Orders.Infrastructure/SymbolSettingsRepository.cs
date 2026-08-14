using Microsoft.EntityFrameworkCore;
using Orders.Core;

namespace Orders.Infrastructure;

public class SymbolSettingsRepository : ISymbolSettingsRepository
{
    private readonly OrdersDbContext _context;

    public SymbolSettingsRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<SymbolSettings> GetOrCreateAsync(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        var existing = await _context.SymbolSettings
            .FirstOrDefaultAsync(s => s.Symbol == normalized);

        if (existing is not null)
            return existing;

        var created = SymbolSettings.CreateDefault(normalized);
        _context.SymbolSettings.Add(created);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two requests racing to create the first row for this symbol — the unique index
            // on Symbol rejects the loser. Whoever lost reads what the winner actually wrote,
            // rather than surfacing a database error for what is, from the caller's point of
            // view, a successful "get or create" (same race handled the same way in
            // AutoQuoteSettingsRepository).
            _context.Entry(created).State = EntityState.Detached;
            return await _context.SymbolSettings.SingleAsync(s => s.Symbol == normalized);
        }

        return created;
    }

    public async Task<IReadOnlyList<string>> GetActiveSymbolsAsync()
    {
        return await _context.SymbolSettings
            .Where(s => s.IsActive)
            .Select(s => s.Symbol)
            .ToListAsync();
    }

    public Task SaveAsync(SymbolSettings settings) => _context.SaveChangesAsync();
}
