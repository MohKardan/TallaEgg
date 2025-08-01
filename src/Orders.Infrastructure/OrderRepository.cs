using Orders.Core;
using Microsoft.EntityFrameworkCore;

namespace Orders.Infrastructure;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order);
    Task<List<Order>> GetOrdersByAssetAsync(string asset);
}

public class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _db;

    public OrderRepository(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<Order> AddAsync(Order order)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<List<Order>> GetOrdersByAssetAsync(string asset)
    {
        return await _db.Orders
            .Where(o => o.Asset == asset)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
}