using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public interface IOrderApiClient
{
    Task<Order?> CreateOrderAsync(string asset, decimal amount, decimal price, Guid userId, string type);
    Task<IEnumerable<Order>> GetOrdersByAssetAsync(string asset);
} 