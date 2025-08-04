using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order);
    Task<List<Order>> GetOrdersByAssetAsync(string asset);
    Task<Order?> GetByIdAsync(Guid orderId);
    Task<Order> UpdateAsync(Order order);
} 