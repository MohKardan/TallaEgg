using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Requests.Wallet;

namespace TallaEgg.Infrastructure.Clients;

/// <summary>
/// HTTP client for communicating with Wallet service
/// کلاینت HTTP برای ارتباط با سرویس کیف پول
/// </summary>
public class WalletApiClient : IWalletApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WalletApiClient> _logger;
    private readonly string _walletApiUrl;

    private readonly string _apiUrl;
    public WalletApiClient(string apiUrl)
    {
        _apiUrl = apiUrl;

        // برای حل مشکل SSL در محیط توسعه
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
    }
    public WalletApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<WalletApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _walletApiUrl = configuration["WalletApiUrl"] ?? "http://localhost:60933";

        // Configure HttpClient base address
        _httpClient.BaseAddress = new Uri(_walletApiUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Get user balance for specific asset
    /// دریافت موجودی کاربر برای دارایی مشخص
    /// </summary>
    public async Task<WalletDTO?> GetBalanceAsync(Guid userId, string asset)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/wallet/balance/{userId}/{asset}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get balance for user {UserId}, asset {Asset}. Status: {Status}",
                    userId, asset, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<WalletDTO>>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for user {UserId}, asset {Asset}", userId, asset);
            return null;
        }
    }

    /// <summary>
    /// Lock balance for order placement
    /// قفل کردن موجودی برای ثبت سفارش
    /// </summary>
    public async Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
        Guid userId, 
        string asset, 
        decimal amount)
    {
        try
        {
            var request = new WalletRequest
            {
                UserId = userId,
                Asset = asset,
                Amount = amount
            };

            var json = JsonSerializer.Serialize(request);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/wallet/lockBalance", stringContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to lock balance for user {UserId}, asset {Asset}, amount {Amount}. Status: {Status}, Response: {Response}",
                    userId, asset, amount, response.StatusCode, responseContent);

                // Try to extract error message from response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<WalletDTO>>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return (false, errorResponse?.Message ?? "خطا در قفل کردن موجودی", null);
                }
                catch
                {
                    return (false, "خطا در قفل کردن موجودی", null);
                }
            }

            var apiResponse = JsonSerializer.Deserialize<ApiResponse<WalletDTO>>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse?.Success == true)
            {
                _logger.LogInformation("Successfully locked {Amount} {Asset} for user {UserId}",
                    amount, asset, userId);
                return (true, apiResponse.Message ?? "موجودی با موفقیت قفل شد", apiResponse.Data);
            }
            else
            {
                return (false, apiResponse?.Message ?? "خطا در قفل کردن موجودی", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, amount);
            return (false, $"خطا در ارتباط با سرویس کیف پول: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Unlock balance when order is cancelled
    /// آزاد کردن موجودی هنگام لغو سفارش
    /// </summary>
    public async Task<(bool Success, string Message)> UnlockBalanceAsync(
        Guid userId, 
        string asset, 
        decimal amount)
    {
        try
        {
            // Note: This endpoint might need to be implemented in Wallet service
            // فراخوانی endpoint آزادسازی موجودی (ممکن است نیاز به پیاده‌سازی در سرویس Wallet داشته باشد)
            var request = new WalletRequest
            {
                UserId = userId,
                Asset = asset,
                Amount = amount
            };

            var json = JsonSerializer.Serialize(request);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Assuming there's an unlock endpoint - if not, we might need to implement it
            var response = await _httpClient.PostAsync("/api/wallet/unlockBalance", stringContent);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully unlocked {Amount} {Asset} for user {UserId}",
                    amount, asset, userId);
                return (true, "موجودی با موفقیت آزاد شد");
            }
            else
            {
                _logger.LogWarning("Failed to unlock balance for user {UserId}, asset {Asset}, amount {Amount}. Status: {Status}",
                    userId, asset, amount, response.StatusCode);
                return (false, "خطا در آزاد کردن موجودی");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, amount);
            return (false, $"خطا در ارتباط با سرویس کیف پول: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate if user has sufficient balance for order
    /// بررسی داشتن موجودی کافی برای سفارش
    /// </summary>
    public async Task<(bool Success, string Message, bool HasSufficientBalance)> ValidateBalanceAsync(
        Guid userId, 
        string asset, 
        decimal amount)
    {
        try
        {
            
            var response = await _httpClient.GetAsync($"/api/wallet/balance/{userId}/{asset}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to validate balance for user {UserId}, asset {Asset}, amount {Amount}. Status: {Status}",
                    userId, asset, amount, response.StatusCode);
                return (false, "خطا در بررسی موجودی", false);
            }

            // Parse the response which should be in the format: { success, message, hasSufficientBalance }
            var validationResult = JsonSerializer.Deserialize<ApiResponse<WalletDTO>>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (validationResult != null)
            {
                bool HasSufficientBalance = validationResult.Data.Balance >= amount;
                return (validationResult.Success, validationResult.Message ?? "", HasSufficientBalance);
            }
            else
            {
                return (false, "خطا در تجزیه پاسخ سرویس", false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, amount);
            return (false, $"خطا در ارتباط با سرویس کیف پول: {ex.Message}", false);
        }
    }


    public async Task<TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>> GetUserWalletsBalanceAsync(Guid userId)
    {
        try
        {

            var response = await _httpClient.GetAsync($"/wallet/balances/{userId}");
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                //var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>>(respText);

                // Parse the response which should be in the format: { success, message, hasSufficientBalance }
                var result = JsonSerializer.Deserialize<ApiResponse<IEnumerable<WalletDTO>>>(respText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result;
            }

            return TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در دریفات اطلاعات");

        }
        catch (Exception ex)
        {
            return TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در ارتباط با سرور");

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
            response = await _httpClient.GetAsync($"/wallet/balance/{userId}/{asset}", cts.Token);

            // Read response content
            responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Handle successful response
                try
                {
                    //var walletDto = JsonConvert.DeserializeObject<ApiResponse<WalletDTO>>(responseContent);
                    
                    // Parse the response which should be in the format: { success, message, hasSufficientBalance }
                    var walletDto = JsonSerializer.Deserialize<ApiResponse<WalletDTO>> (responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
                        //var errorResponse = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<object>>(responseContent);

                        // Parse the response which should be in the format: { success, message, hasSufficientBalance }
                        var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
            //var json = JsonConvert.SerializeObject(request);
            //var content = new StringContent(json, Encoding.UTF8, "application/json");

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/wallet/deposit", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                //var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>>(respText);

                // Parse the response which should be in the format: { success, message, hasSufficientBalance }
                var result = JsonSerializer.Deserialize<ApiResponse<WalletBallanceDTO>>(respText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
            //var json = JsonConvert.SerializeObject(request);
            //var content = new StringContent(json, Encoding.UTF8, "application/json");

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/wallet/withdrawal", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                //var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>>(respText);

                // Parse the response which should be in the format: { success, message, hasSufficientBalance }
                var result = JsonSerializer.Deserialize<ApiResponse<WalletBallanceDTO>>(respText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
