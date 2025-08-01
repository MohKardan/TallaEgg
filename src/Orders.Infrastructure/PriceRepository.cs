using Microsoft.EntityFrameworkCore;
using Orders.Core;

namespace Orders.Infrastructure;

public class PriceRepository : IPriceRepository
{
    private readonly OrdersDbContext _context;

    public PriceRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<Price?> GetByAssetAsync(string asset)
    {
        return await _context.Prices
            .FirstOrDefaultAsync(p => p.Asset == asset);
    }

    public async Task<Price> CreateAsync(Price price)
    {
        _context.Prices.Add(price);
        await _context.SaveChangesAsync();
        return price;
    }

    public async Task<Price> UpdateAsync(Price price)
    {
        _context.Prices.Update(price);
        await _context.SaveChangesAsync();
        return price;
    }

    public async Task<IEnumerable<Price>> GetAllAsync()
    {
        return await _context.Prices.ToListAsync();
    }
} 