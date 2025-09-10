using Orders.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;

namespace Orders.Application;

public class TradeService
{
    private readonly ITradeRepository _tradeRepository;

    public TradeService(ITradeRepository tradeRepository)
    {
        _tradeRepository = tradeRepository;
    }

    public async Task<Trade?> GetTradeByIdAsync(Guid id)
    {
        return await _tradeRepository.GetByIdAsync(id);
    }

    public async Task<List<Trade>> GetTradesBySymbolAsync(string symbol)
    {
        return await _tradeRepository.GetTradesBySymbolAsync(symbol);
    }

    public async Task<List<Trade>> GetTradesByBuyOrderIdAsync(Guid buyOrderId)
    {
        return await _tradeRepository.GetTradesByBuyOrderIdAsync(buyOrderId);
    }

    public async Task<List<Trade>> GetTradesBySellOrderIdAsync(Guid sellOrderId)
    {
        return await _tradeRepository.GetTradesBySellOrderIdAsync(sellOrderId);
    }

    public async Task<List<Trade>> GetTradesByBuyerUserIdAsync(Guid buyerUserId)
    {
        return await _tradeRepository.GetTradesByBuyerUserIdAsync(buyerUserId);
    }

    public async Task<List<Trade>> GetTradesBySellerUserIdAsync(Guid sellerUserId)
    {
        return await _tradeRepository.GetTradesBySellerUserIdAsync(sellerUserId);
    }

    public async Task<List<Trade>> GetTradesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _tradeRepository.GetTradesByDateRangeAsync(startDate, endDate);
    }

    public async Task<(List<Trade> Trades, int TotalCount)> GetTradesPaginatedAsync(
        int pageNumber = 1, 
        int pageSize = 10,
        Guid? buyOrderId = null,
        Guid? sellOrderId = null,
        string? symbol = null,
        Guid? buyerUserId = null,
        Guid? sellerUserId = null)
    {
        return await _tradeRepository.GetTradesPaginatedAsync(
            pageNumber, pageSize, buyOrderId, sellOrderId, symbol, buyerUserId, sellerUserId);
    }

    public async Task<PagedResult<TradeHistoryDto>> GetTradesByUserIdAsync(Guid userId, int pageNumber, int pageSize)
    {
        return await _tradeRepository.GetTradesByUserIdAsync(userId, pageNumber, pageSize);
    }

    public async Task<bool> DeleteTradeAsync(Guid id)
    {
        return await _tradeRepository.DeleteAsync(id);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _tradeRepository.ExistsAsync(id);
    }
}
