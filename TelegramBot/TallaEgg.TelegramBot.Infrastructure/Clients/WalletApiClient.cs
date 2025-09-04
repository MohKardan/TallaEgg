using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs;
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
        // Input validation
        if (userId == Guid.Empty)
        {
            return (false, null, "شناسه کاربر نامعتبر است.");
        }

        if (string.IsNullOrWhiteSpace(asset))
        {
            return (false, null, "نوع دارایی مشخص نشده است.");
        }

        HttpResponseMessage? response = null;
        string? responseContent = null;

        try
        {
            // Create cancellation token with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            // Make HTTP request with timeout
            response = await _httpClient.GetAsync($"{_apiUrl}/wallet/balance/{userId}/{asset}", cts.Token);
            
            // Read response content
            responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                // Handle successful response
                try
                {
                    var walletDto = JsonConvert.DeserializeObject<ApiResponse<WalletDTO>>(responseContent);
                    return (true, walletDto.Data.Balance, "موجودی دریافت شد.");
                }
                catch (JsonException jsonEx)
                {
                    return (false, null, $"خطا در پردازش اطلاعات دریافتی: پاسخ سرور قابل تفسیر نیست.");
                }
            }
            else
            {
                // Handle HTTP error status codes
                var errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "کیف پول مورد نظر یافت نشد.",
                    System.Net.HttpStatusCode.Unauthorized => "عدم دسترسی: احراز هویت نشده است.",
                    System.Net.HttpStatusCode.Forbidden => "عدم دسترسی: دسترسی به این عملیات مجاز نیست.",
                    System.Net.HttpStatusCode.BadRequest => "درخواست نامعتبر: پارامترهای ارسالی صحیح نیست.",
                    System.Net.HttpStatusCode.InternalServerError => "خطای داخلی سرور.",
                    System.Net.HttpStatusCode.ServiceUnavailable => "سرویس کیف پول در دسترس نیست.",
                    System.Net.HttpStatusCode.RequestTimeout => "زمان انتظار درخواست به پایان رسید.",
                    System.Net.HttpStatusCode.TooManyRequests => "تعداد درخواست‌های زیاد. لطفاً کمی صبر کنید.",
                    _ => $"خطا در دریافت موجودی: کد خطا {(int)response.StatusCode}"
                };

                // Try to extract detailed error message from response if available
                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<object>>(responseContent);
                        if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.Message))
                        {
                            errorMessage = errorResponse.Message;
                        }
                    }
                    catch
                    {
                        // If parsing fails, use the default error message
                    }
                }

                return (false, null, errorMessage);
            }
        }
        catch (HttpRequestException httpEx)
        {
            // Network-related errors
            return (false, null, $"خطا در ارتباط شبکه: {httpEx.Message}");
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            // Request timeout
            return (false, null, "زمان انتظار درخواست به پایان رسید. لطفاً مجدداً تلاش کنید.");
        }
        catch (TaskCanceledException)
        {
            // Request was cancelled
            return (false, null, "درخواست لغو شد.");
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled
            return (false, null, "عملیات لغو شد.");
        }
        catch (JsonException jsonEx)
        {
            // JSON parsing errors
            return (false, null, "خطا در پردازش اطلاعات دریافتی از سرور.");
        }
        catch (ArgumentException argEx)
        {
            // Invalid arguments
            return (false, null, $"پارامتر نامعتبر: {argEx.Message}");
        }
        catch (InvalidOperationException invOpEx)
        {
            // Invalid operation state
            return (false, null, $"عملیات غیرمجاز: {invOpEx.Message}");
        }
        catch (Exception ex)
        {
            // Catch-all for any other unexpected exceptions
            return (false, null, $"خطای غیرمنتظره در ارتباط با سرور: {ex.Message}");
        }
        finally
        {
            // Cleanup resources if needed
            response?.Dispose();
        }
    }

    public async Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request)
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

    public async Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> WithdrawalAsync(WalletRequest request)
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
