namespace Orders.Core;

public interface IOrderRepository
{
    // Create
    Task<Order> AddAsync(Order order);
    
    // Read
    Task<Order?> GetByIdAsync(Guid id);
    Task<List<Order>> GetOrdersByAssetAsync(string asset);
    Task<List<Order>> GetOrdersByUserIdAsync(Guid userId);
    Task<List<Order>> GetOrdersByStatusAsync(OrderStatus status);
    Task<List<Order>> GetOrdersByTypeAsync(OrderType type);
    Task<List<Order>> GetOrdersByTradingTypeAsync(TradingType tradingType);
    Task<List<Order>> GetOrdersByRoleAsync(OrderRole role);
    Task<List<Order>> GetActiveOrdersAsync();
    Task<List<Order>> GetOrdersByDateRangeAsync(DateTime from, DateTime to);
    Task<List<Order>> GetAvailableMakerOrdersAsync(string asset, TradingType tradingType);
    Task<int> GetOrderCountByAssetAsync(string asset);
    Task<decimal> GetTotalValueByAssetAsync(string asset);
    
    // Update
    Task<Order> UpdateAsync(Order order);
    Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus status, string? notes = null);
    
    // Delete
    Task<bool> DeleteAsync(Guid id);
    
    // Exists
    Task<bool> ExistsAsync(Guid id);
    
    // Pagination
    Task<(List<Order> Orders, int TotalCount)> GetOrdersPaginatedAsync(
        int pageNumber, 
        int pageSize, 
        string? asset = null, 
        OrderType? type = null, 
        OrderStatus? status = null,
        TradingType? tradingType = null,
        OrderRole? role = null);
} 