using Microsoft.EntityFrameworkCore;
using Orders.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;

namespace Orders.Infrastructure;

public class TradeRepository : ITradeRepository
{
    private readonly OrdersDbContext _context;

    public TradeRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<Trade> AddAsync(Trade trade)
    {
        await _context.Trades.AddAsync(trade);
        await _context.SaveChangesAsync();
        return trade;
    }

    public async Task<Trade?> GetByIdAsync(Guid id)
    {
        return await _context.Trades.FindAsync(id);
    }

    public async Task<Trade> UpdateAsync(Trade trade)
    {
        _context.Trades.Update(trade);
        await _context.SaveChangesAsync();
        return trade;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var trade = await _context.Trades.FindAsync(id);
        if (trade == null)
            return false;

        _context.Trades.Remove(trade);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Trades.AnyAsync(t => t.Id == id);
    }

    public async Task<List<Trade>> GetTradesByBuyOrderIdAsync(Guid buyOrderId)
    {
        return await _context.Trades
            .Where(t => t.BuyOrderId == buyOrderId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Trade>> GetTradesBySellOrderIdAsync(Guid sellOrderId)
    {
        return await _context.Trades
            .Where(t => t.SellOrderId == sellOrderId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Trade>> GetTradesBySymbolAsync(string symbol)
    {
        return await _context.Trades
            .Where(t => t.Symbol == symbol)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Trade>> GetTradesByBuyerUserIdAsync(Guid buyerUserId)
    {
        return await _context.Trades
            .Where(t => t.BuyerUserId == buyerUserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Trade>> GetTradesBySellerUserIdAsync(Guid sellerUserId)
    {
        return await _context.Trades
            .Where(t => t.SellerUserId == sellerUserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Trade>> GetTradesByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _context.Trades
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<Trade> Trades, int TotalCount)> GetTradesPaginatedAsync(
        int pageNumber, 
        int pageSize, 
        Guid? buyOrderId = null, 
        Guid? sellOrderId = null,
        string? symbol = null,
        Guid? buyerUserId = null,
        Guid? sellerUserId = null)
    {
        var query = _context.Trades.AsQueryable();

        if (buyOrderId.HasValue)
            query = query.Where(t => t.BuyOrderId == buyOrderId.Value);

        if (sellOrderId.HasValue)
            query = query.Where(t => t.SellOrderId == sellOrderId.Value);

        if (!string.IsNullOrEmpty(symbol))
            query = query.Where(t => t.Symbol == symbol);

        if (buyerUserId.HasValue)
            query = query.Where(t => t.BuyerUserId == buyerUserId.Value);

        if (sellerUserId.HasValue)
            query = query.Where(t => t.SellerUserId == sellerUserId.Value);

        var totalCount = await query.CountAsync();

        var trades = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (trades, totalCount);
    }

    public async Task<PagedResult<TradeHistoryDto>> GetTradesByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize)
    {
        var query = _context.Trades
            .Where(t => t.BuyerUserId == userId || t.SellerUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TradeHistoryDto
            {
                Id = t.Id,
                Symbol = t.Symbol,
                Price = t.Price,
                Quantity = t.Quantity,
                QuoteQuantity = t.QuoteQuantity,
                BuyerUserId = t.BuyerUserId,
                SellerUserId = t.SellerUserId,
                MakerUserId = t.MakerUserId,
                TakerUserId = t.TakerUserId,
                FeeBuyer = t.FeeBuyer,
                FeeSeller = t.FeeSeller,
                MakerFee = t.MakerFee,
                TakerFee = t.TakerFee,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            });

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<TradeHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
