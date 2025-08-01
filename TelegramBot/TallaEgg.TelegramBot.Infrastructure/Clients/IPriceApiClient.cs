using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public interface IPriceApiClient
{
    Task<Price?> GetLatestPriceAsync(string asset);
    Task<IEnumerable<Price>> GetAllPricesAsync();
} 