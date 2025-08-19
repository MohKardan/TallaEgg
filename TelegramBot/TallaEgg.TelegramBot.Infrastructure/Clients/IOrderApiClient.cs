using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public interface IOrderApiClient
{
    
    Task<ApiResponse<PagedResult<OrderHistoryDto>>> GetUserOrdersAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<(bool success, string message)> SubmitOrderAsync(OrderDto order);
    Task<(bool success, string message)> CancelOrderAsync(Guid orderId);
    Task<BestBidAskResult?> GetBestBidAskAsync(string asset, TradingType tradingType);
    
    Task<ApiResponse<bool>> NotifyMatchingEngineAsync(NotifyMatchingEngineRequest request);
} 