namespace Orders.Core;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order);
    Task<List<Order>> GetOrdersByAssetAsync(string asset);
} 