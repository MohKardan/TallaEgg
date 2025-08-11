using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot;

public class WalletApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient = new();

    public WalletApiClient(string apiUrl)
    {
        _apiUrl = apiUrl;
    }

    public async Task<(bool success, List<WalletDto>? wallets, string message)> GetUserWalletsAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/api/wallet/user/{userId}");
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var wallets = JsonConvert.DeserializeObject<List<WalletDto>>(respText);
                return (true, wallets, "موجودی دریافت شد.");
            }
            
            return (false, null, $"خطا در دریافت موجودی: {respText}");
        }
        catch (Exception ex)
        {
            return (false, null, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    public async Task<(bool success, decimal? balance, string message)> GetWalletBalanceAsync(Guid userId, string asset)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/api/wallet/balance/{userId}/{asset}");
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var balance = JsonConvert.DeserializeObject<decimal>(respText);
                return (true, balance, "موجودی دریافت شد.");
            }
            
            return (false, null, $"خطا در دریافت موجودی: {respText}");
        }
        catch (Exception ex)
        {
            return (false, null, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }
}

public class WalletDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal LockedBalance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
