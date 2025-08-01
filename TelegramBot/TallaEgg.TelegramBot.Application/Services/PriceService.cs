using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Application.Services;

public class PriceService : IPriceService
{
    private readonly IPriceRepository _priceRepository;

    public PriceService(IPriceRepository priceRepository)
    {
        _priceRepository = priceRepository;
    }

    public async Task<Price?> GetLatestPriceAsync(string asset)
    {
        return await _priceRepository.GetByAssetAsync(asset);
    }

    public async Task<IEnumerable<Price>> GetAllPricesAsync()
    {
        return await _priceRepository.GetAllAsync();
    }

    public async Task<Price> UpdatePriceAsync(string asset, decimal buyPrice, decimal sellPrice, string source = "Manual")
    {
        var existingPrice = await _priceRepository.GetByAssetAsync(asset);
        
        if (existingPrice != null)
        {
            existingPrice.BuyPrice = buyPrice;
            existingPrice.SellPrice = sellPrice;
            existingPrice.UpdatedAt = DateTime.UtcNow;
            existingPrice.Source = source;
            return await _priceRepository.UpdateAsync(existingPrice);
        }
        else
        {
            var newPrice = new Price
            {
                Id = Guid.NewGuid(),
                Asset = asset,
                BuyPrice = buyPrice,
                SellPrice = sellPrice,
                UpdatedAt = DateTime.UtcNow,
                Source = source
            };
            return await _priceRepository.CreateAsync(newPrice);
        }
    }
} 