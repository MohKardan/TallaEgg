using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.TelegramBot.Infrastructure.Repositories;

public class PriceRepository : IPriceRepository
{
    private readonly IPriceApiClient _priceApiClient;

    public PriceRepository(IPriceApiClient priceApiClient)
    {
        _priceApiClient = priceApiClient;
    }

    public async Task<Price?> GetByAssetAsync(string asset)
    {
        return await _priceApiClient.GetLatestPriceAsync(asset);
    }

    public async Task<Price> CreateAsync(Price price)
    {
        // This would need to be implemented in the API
        throw new NotImplementedException();
    }

    public async Task<Price> UpdateAsync(Price price)
    {
        // This would need to be implemented in the API
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Price>> GetAllAsync()
    {
        return await _priceApiClient.GetAllPricesAsync();
    }
} 