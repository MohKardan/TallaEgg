namespace Matching.Core;

public interface IMatchingRepository
{
    // Order operations
    Task<Order> CreateOrderAsync(Order order);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<Order?> GetOrderByExternalIdAsync(string externalOrderId);
    Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId, string? asset = null);
    Task<IEnumerable<Order>> GetPendingOrdersAsync(string asset, OrderType type);
    Task<Order> UpdateOrderAsync(Order order);
    Task<bool> CancelOrderAsync(Guid orderId);
    
    // Trade operations
    Task<Trade> CreateTradeAsync(Trade trade);
    Task<IEnumerable<Trade>> GetUserTradesAsync(Guid userId, string? asset = null);
    Task<IEnumerable<Trade>> GetAssetTradesAsync(string asset, DateTime? fromDate = null);
    
    // Market data
    Task<IEnumerable<Order>> GetOrderBookAsync(string asset, int depth = 10);
    Task<decimal> GetLastPriceAsync(string asset);
    Task<IEnumerable<Trade>> GetRecentTradesAsync(string asset, int count = 50);
} 