using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public interface IOrderApiClient
{
    
    Task<ApiResponse<PagedResult<OrderHistoryDto>>> GetUserOrdersAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<ApiResponse<PagedResult<TradeHistoryDto>>> GetUserTradesAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<(bool success, string message)> SubmitOrderAsync(OrderDto order);
    Task<(bool success, string message)> CancelOrderAsync(Guid orderId);
    
    /// <summary>
    /// کنسل کردن تمام سفارشات فعال یک کاربر
    /// </summary>
    /// <param name="userId">شناسه کاربر</param>
    /// <param name="reason">دلیل کنسل کردن (اختیاری)</param>
    /// <returns>نتیجه عملیات شامل موفقیت، پیام و تعداد سفارشات کنسل شده</returns>
    Task<(bool success, string message, int cancelledCount)> CancelAllUserActiveOrdersAsync(Guid userId, string? reason = null);
    
    Task<ApiResponse<bool>> NotifyMatchingEngineAsync(NotifyMatchingEngineRequest request);
} 