using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public class PriceApiClient : IPriceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public PriceApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["PricesApiUrl"] ?? "http://localhost:5135/api";
    }

    public async Task<Price?> GetLatestPriceAsync(string asset)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/prices/{asset}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Price>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Price>> GetAllPricesAsync()
    {
        var prices = new List<Price>
        {
            new Price
            {
                Id = Guid.NewGuid(),
                Asset = "BTC/USD",
                BuyPrice = 65000.50m,
                SellPrice = 64995.25m,
                UpdatedAt = DateTime.UtcNow,
                Source = "Binance"
            },
            new Price
            {
                Id = Guid.NewGuid(),
                Asset = "ETH/USD",
                BuyPrice = 3500.75m,
                SellPrice = 3498.10m,
                UpdatedAt = DateTime.UtcNow,
                Source = "Coinbase"
            },
            new Price
            {
                Id = Guid.NewGuid(),
                Asset = "AAPL",
                BuyPrice = 195.30m,
                SellPrice = 195.25m,
                UpdatedAt = DateTime.UtcNow,
                Source = "Yahoo Finance"
            },
            new Price
            {
                Id = Guid.NewGuid(),
                Asset = "EUR/USD",
                BuyPrice = 1.0850m,
                SellPrice = 1.0848m,
                UpdatedAt = DateTime.UtcNow,
                Source = "FXCM"
            }
        };

        return prices;
        /// DOTO
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/prices");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<IEnumerable<Price>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Price>();
            }

            return Enumerable.Empty<Price>();
        }
        catch (Exception)
        {
            return Enumerable.Empty<Price>();
        }
    }
} 