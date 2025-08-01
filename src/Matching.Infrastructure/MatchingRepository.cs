using Microsoft.EntityFrameworkCore;
using Matching.Core;

namespace Matching.Infrastructure;

public class MatchingRepository : IMatchingRepository
{
    private readonly MatchingDbContext _context;

    public MatchingRepository(MatchingDbContext context)
    {
        _context = context;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        return await _context.Orders.FindAsync(orderId);
    }

    public async Task<Order?> GetOrderByExternalIdAsync(string externalOrderId)
    {
        return await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == externalOrderId);
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId, string? asset = null)
    {
        var query = _context.Orders.Where(o => o.UserId == userId);
        if (!string.IsNullOrEmpty(asset))
            query = query.Where(o => o.Asset == asset);
        
        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetPendingOrdersAsync(string asset, OrderType type)
    {
        return await _context.Orders
            .Where(o => o.Asset == asset && o.Type == type && o.Status == OrderStatus.Pending)
            .OrderBy(o => type == OrderType.Buy ? o.Price : -o.Price) // Buy orders by price ascending, Sell by descending
            .ToListAsync();
    }

    public async Task<Order> UpdateOrderAsync(Order order)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<bool> CancelOrderAsync(Guid orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null || order.Status != OrderStatus.Pending)
            return false;

        order.Status = OrderStatus.Cancelled;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Trade> CreateTradeAsync(Trade trade)
    {
        _context.Trades.Add(trade);
        await _context.SaveChangesAsync();
        return trade;
    }

    public async Task<IEnumerable<Trade>> GetUserTradesAsync(Guid userId, string? asset = null)
    {
        var query = _context.Trades.Where(t => t.BuyerUserId == userId || t.SellerUserId == userId);
        if (!string.IsNullOrEmpty(asset))
            query = query.Where(t => t.Asset == asset);
        
        return await query.OrderByDescending(t => t.ExecutedAt).ToListAsync();
    }

    public async Task<IEnumerable<Trade>> GetAssetTradesAsync(string asset, DateTime? fromDate = null)
    {
        var query = _context.Trades.Where(t => t.Asset == asset);
        if (fromDate.HasValue)
            query = query.Where(t => t.ExecutedAt >= fromDate.Value);
        
        return await query.OrderByDescending(t => t.ExecutedAt).ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrderBookAsync(string asset, int depth = 10)
    {
        var buyOrders = await _context.Orders
            .Where(o => o.Asset == asset && o.Type == OrderType.Buy && o.Status == OrderStatus.Pending)
            .OrderByDescending(o => o.Price)
            .Take(depth)
            .ToListAsync();

        var sellOrders = await _context.Orders
            .Where(o => o.Asset == asset && o.Type == OrderType.Sell && o.Status == OrderStatus.Pending)
            .OrderBy(o => o.Price)
            .Take(depth)
            .ToListAsync();

        return buyOrders.Concat(sellOrders);
    }

    public async Task<decimal> GetLastPriceAsync(string asset)
    {
        var lastTrade = await _context.Trades
            .Where(t => t.Asset == asset)
            .OrderByDescending(t => t.ExecutedAt)
            .FirstOrDefaultAsync();

        return lastTrade?.Price ?? 0;
    }

    public async Task<IEnumerable<Trade>> GetRecentTradesAsync(string asset, int count = 50)
    {
        return await _context.Trades
            .Where(t => t.Asset == asset)
            .OrderByDescending(t => t.ExecutedAt)
            .Take(count)
            .ToListAsync();
    }
} 