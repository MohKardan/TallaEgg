using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text.Json;
using TallaEgg.TelegramBot.Core.Models;
using TallaEgg.Core;

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
                return System.Text.Json.JsonSerializer.Deserialize<Price>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Price>> GetAllPricesAsync0()
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
                return System.Text.Json.JsonSerializer.Deserialize<IEnumerable<Price>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Price>();
            }

            return Enumerable.Empty<Price>();
        }
        catch (Exception)
        {
            return Enumerable.Empty<Price>();
        }
    }

    public async Task<(bool success, List<PriceDto>? prices)> GetAllPricesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/prices");
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var prices = JsonConvert.DeserializeObject<List<PriceDto>>(respText);
                return (true, prices);
            }
            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, null);
        }
    }

    Task<IEnumerable<Price>> IPriceApiClient.GetAllPricesAsync()
    {
        throw new NotImplementedException();
    }
}
public class PriceDto
{
    public Guid Id { get; set; }
    public string Asset { get; set; } = "";
    public decimal BuyPrice { get; set; }
    public decimal SellPrice { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Source { get; set; } = "";
}