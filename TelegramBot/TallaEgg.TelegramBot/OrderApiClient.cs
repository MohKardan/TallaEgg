using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot;

public class OrderApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient = new();

    public OrderApiClient(string apiUrl)
    {
        _apiUrl = apiUrl;
    }

    public async Task<(bool success, string message)> SubmitOrderAsync(OrderDto order)
    {
        var json = JsonConvert.SerializeObject(order);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            var response = await _httpClient.PostAsync(_apiUrl, content);
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
    public string Type { get; set; } = "BUY";
}