using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string asset, decimal amount, decimal price, Guid userId, string type);
    Task<IEnumerable<Order>> GetOrdersByAssetAsync(string asset);
    Task<Order?> GetOrderByIdAsync(Guid orderId);
} 