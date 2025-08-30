using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Requests.Wallet;
using Telegram.Bot.Requests.Abstractions;

namespace TallaEgg.TelegramBot;

public class WalletApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient = new();

    public WalletApiClient(string apiUrl)
    {
        _apiUrl = apiUrl;
        
        // برای حل مشکل SSL در محیط توسعه
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
    }

    public async Task<TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>> GetUserWalletsBalanceAsync(Guid userId)
    {
        try
        {

            var response = await _httpClient.GetAsync($"{_apiUrl}/wallet/balances/{userId}");
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>>(respText);
                return result;
            }

            return TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در دریفات اطلاعات");

        }
        catch (Exception ex)
        {
            return TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در ارتباط با سرور");

        }
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

    public async Task<BalanceValidationResult> ValidateBalanceForMarketOrderAsync(Guid userId, string asset, decimal amount, int orderType)
    {
        try
        {
            var request = new
            {
                UserId = userId,
                Asset = asset,
                Amount = amount,
                OrderType = orderType
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_apiUrl}/api/wallet/market/validate-balance", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<BalanceValidationResult>(respText);
                return result ?? new BalanceValidationResult { Success = false, Message = "خطا در پردازش پاسخ" };
            }

            return new BalanceValidationResult { Success = false, Message = $"خطا در بررسی موجودی: {respText}" };
        }
        catch (Exception ex)
        {
            return new BalanceValidationResult { Success = false, Message = $"خطا در ارتباط با سرور: {ex.Message}" };
        }
    }

    public async Task<(bool success, string message)> UpdateBalanceForMarketOrderAsync(UpdateBalanceForMarketOrderRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_apiUrl}/api/wallet/market/update-balance", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, "موجودی با موفقیت به‌روزرسانی شد.");
            }

            return (false, $"خطا در به‌روزرسانی موجودی: {respText}");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    public async Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletBallanceChangeRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_apiUrl}/wallet/deposit", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>>(respText);
                return result;
            }

            return TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Fail("خطا در بروزرسانی");

        }
        catch (Exception ex)
        {
            return TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Fail("خطا در ارتباط با سرور");

        }
    }


    public async Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> WithdrawalAsync(WalletBallanceChangeRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_apiUrl}/wallet/withdrawal", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>>(respText);
                return result;
            }

            return TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Fail("خطا در بروزرسانی");

        }
        catch (Exception ex)
        {
            return TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Fail("خطا در ارتباط با سرور");

        }
    }


}

public class BalanceValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool HasSufficientBalance { get; set; }
}

public class UpdateBalanceForMarketOrderRequest
{
    public Guid UserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public int OrderType { get; set; }
    public Guid OrderId { get; set; }
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
