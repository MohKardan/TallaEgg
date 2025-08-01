using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OrderApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["OrderApiUrl"] ?? "http://localhost:5135/api";
    }

    public async Task<Order?> CreateOrderAsync(string asset, decimal amount, decimal price, Guid userId, string type)
    {
        try
        {
            var request = new
            {
                Asset = asset,
                Amount = amount,
                Price = price,
                UserId = userId,
                Type = type
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/order", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Order>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Order>> GetOrdersByAssetAsync(string asset)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/orders/{asset}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<IEnumerable<Order>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Enumerable.Empty<Order>();
            }

            return Enumerable.Empty<Order>();
        }
        catch (Exception)
        {
            return Enumerable.Empty<Order>();
        }
    }
} 