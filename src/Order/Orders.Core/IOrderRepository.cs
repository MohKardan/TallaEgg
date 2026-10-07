using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;

namespace Orders.Core;

public interface IOrderRepository
{
    // Create
    Task<Order> AddAsync(Order order);
    
    // Read
    Task<Order?> GetByIdAsync(Guid id);
    /// <summary>
    /// The open book for one symbol and market: the orders <see cref="Order.IsActive"/> would accept,
    /// filtered by the database rather than loaded and discarded (issue #280).
    /// </summary>
    Task<List<Order>> GetActiveOrdersByAssetAsync(string asset, TradingType tradingType);
    Task<PagedResult<OrderHistoryDto>> GetOrdersByUserIdAsync(Guid userId, int pageNumber,int pageSize);
    Task<List<Order>> GetOrdersByStatusAsync(OrderStatus status);
    Task<List<Order>> GetOrdersByTypeAsync(OrderSide type);
    Task<List<Order>> GetOrdersByTradingTypeAsync(TradingType tradingType);
    Task<List<Order>> GetOrdersByRoleAsync(OrderRole role);
    
    /// <summary>
    /// Returns every active order belonging to one user.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>The user's active orders.</returns>
    Task<List<Order>> GetActiveOrdersByUserIdAsync(Guid userId);
    
    /// <summary>
    /// Returns every active order in the system.
    /// </summary>
    /// <returns>All active orders.</returns>
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
        OrderSide? type = null, 
        OrderStatus? status = null,
        TradingType? tradingType = null,
        OrderRole? role = null);
} 