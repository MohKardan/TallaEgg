using Microsoft.EntityFrameworkCore;
using TallaEgg.Api.Modules.Orders.Core;
using TallaEgg.Api.Shared.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Api.Modules.Orders.Infrastructure;

public class OrderRepository : IOrderRepository
{
    private readonly TallaEggDbContext _context;

    public OrderRepository(TallaEggDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id)
    {
        return await _context.Orders.FindAsync(id);
    }

    public async Task<IEnumerable<Order>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetByAssetAsync(string asset)
    {
        return await _context.Orders
            .Where(o => o.Asset == asset)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
    {
        return await _context.Orders
            .Where(o => o.Status == status)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order> CreateAsync(Order order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<Order> UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return false;

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Order>> GetActiveOrdersAsync()
    {
        return await _context.Orders
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetCompletedOrdersAsync()
    {
        return await _context.Orders
            .Where(o => o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
}
