using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
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
                Type = type,
                TradingType = "Spot" // Default to Spot trading
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/orders", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<OrderResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result?.Order;
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/orders/asset/{asset}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OrdersResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result?.Orders ?? Enumerable.Empty<Order>();
            }

            return Enumerable.Empty<Order>();
        }
        catch (Exception)
        {
            return Enumerable.Empty<Order>();
        }
    }
    public async Task<(bool success, string message)> SubmitOrderAsync(OrderDto order)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(order);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/orders", content);
            var respText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return (true, "سفارش شما ثبت شد.");
            return (false, $"خطا در ثبت سفارش: {respText}");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }
}

public class OrderDto
{
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "Buy"; // "Buy" or "Sell"
    public string TradingType { get; set; } = "Spot"; // "Spot" or "Futures"
}

public class OrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Order? Order { get; set; }
}

public class OrdersResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public IEnumerable<Order> Orders { get; set; } = Enumerable.Empty<Order>();
}