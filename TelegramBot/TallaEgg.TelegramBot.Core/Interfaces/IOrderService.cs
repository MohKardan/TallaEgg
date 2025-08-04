using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string asset, decimal amount, decimal price, Guid userId, string type);
    Task<IEnumerable<Order>> GetOrdersByAssetAsync(string asset);
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task<IEnumerable<Order>> GetUserActiveOrdersAsync(Guid userId);
    Task<Order> CancelOrderAsync(Guid orderId, Guid userId, string reason = "Cancelled by user");
    Task<bool> CancelAllUserOrdersAsync(Guid userId, string reason = "Cancelled by user");
    Task<bool> HasActiveOrdersAsync(Guid userId);
    Task<bool> CanUserCreateOrderAsync(Guid userId);
} 