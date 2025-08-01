using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IPriceService
{
    Task<Price?> GetLatestPriceAsync(string asset);
    Task<IEnumerable<Price>> GetAllPricesAsync();
    Task<Price> UpdatePriceAsync(string asset, decimal buyPrice, decimal sellPrice, string source = "Manual");
} 