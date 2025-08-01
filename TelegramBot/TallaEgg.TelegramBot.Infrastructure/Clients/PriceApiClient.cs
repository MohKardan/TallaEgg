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