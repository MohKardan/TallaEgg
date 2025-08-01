using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot;

public class PriceApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient = new();

    public PriceApiClient(string apiUrl)
    {
        _apiUrl = apiUrl;
    }

    public async Task<(bool success, PriceDto? price)> GetLatestPriceAsync(string asset)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/prices/{asset}");
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var price = JsonConvert.DeserializeObject<PriceDto>(respText);
                return (true, price);
            }
            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, null);
        }
    }

    public async Task<(bool success, List<PriceDto>? prices)> GetAllPricesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/prices");
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