using TallaEgg.Core.DTOs;

namespace Orders.Core;

public interface ITradeRepository
{
    // Create
    Task<Trade> AddAsync(Trade trade);
    
    // Read
    Task<Trade?> GetByIdAsync(Guid id);
    Task<List<Trade>> GetTradesByOrderIdAsync(Guid orderId);
    Task<List<Trade>> GetTradesBySymbolIdAsync(Guid symbolId);
    Task<List<Trade>> GetTradesByDateRangeAsync(DateTime from, DateTime to);
    
    // Update
    Task<Trade> UpdateAsync(Trade trade);
    
    // Delete
    Task<bool> DeleteAsync(Guid id);
    
    // Exists
    Task<bool> ExistsAsync(Guid id);
    
    // Pagination
    Task<(List<Trade> Trades, int TotalCount)> GetTradesPaginatedAsync(
        int pageNumber, 
        int pageSize, 
        Guid? orderId = null, 
        Guid? symbolId = null);
}
