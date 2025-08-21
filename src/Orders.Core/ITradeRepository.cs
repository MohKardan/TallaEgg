using TallaEgg.Core.DTOs;

namespace Orders.Core;

public interface ITradeRepository
{
    // Create
    Task<Trade> AddAsync(Trade trade);
    
    // Read
    Task<Trade?> GetByIdAsync(Guid id);
    Task<List<Trade>> GetTradesByBuyOrderIdAsync(Guid buyOrderId);
    Task<List<Trade>> GetTradesBySellOrderIdAsync(Guid sellOrderId);
    Task<List<Trade>> GetTradesBySymbolAsync(string symbol);
    Task<List<Trade>> GetTradesByBuyerUserIdAsync(Guid buyerUserId);
    Task<List<Trade>> GetTradesBySellerUserIdAsync(Guid sellerUserId);
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
        Guid? buyOrderId = null, 
        Guid? sellOrderId = null,
        string? symbol = null,
        Guid? buyerUserId = null,
        Guid? sellerUserId = null);
}
