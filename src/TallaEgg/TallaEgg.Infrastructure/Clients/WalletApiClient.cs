using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.Core.Responses.Order;

namespace TallaEgg.Infrastructure.Clients;

/// <summary>
/// HTTP client for communicating with Wallet service
/// HTTP client for the Wallet service.
/// </summary>
public class WalletApiClient : IWalletApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WalletApiClient>? _logger;
    private readonly string? _walletApiUrl;

    public WalletApiClient(string? apiUrl)
    {

        var handler = new HttpClientHandler();
#if DEBUG
        // DEV ONLY: accept self-signed certs for local inter-service calls.
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
        _httpClient = new HttpClient(handler);

        _walletApiUrl = apiUrl ?? "http://localhost:60933/api";
        _httpClient.BaseAddress = new Uri(_walletApiUrl);
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", APIKeyConstant.TallaEggApiKey);
    }
    public WalletApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<WalletApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _walletApiUrl = configuration["WalletApiUrl"] ?? "http://localhost:60933/api";

        // Configure HttpClient base address
        _httpClient.BaseAddress = new Uri(_walletApiUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", APIKeyConstant.TallaEggApiKey);
    }

    /// <summary>
    /// Lock balance for order placement
    /// Locks balance when placing an order.
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

            var response = await _httpClient.PostAsync("api/wallet/lockBalance", stringContent);
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
    /// Releases locked balance when an order is cancelled.
    /// </summary>
    public async Task<(bool Success, string Message)> UnlockBalanceAsync(
        Guid userId,
        string asset,
        decimal amount)
    {
        try
        {
            // Note: This endpoint might need to be implemented in Wallet service
            // Calls the wallet's unlock endpoint.
            var request = new WalletRequest
            {
                UserId = userId,
                Asset = asset,
                Amount = amount
            };

            var json = JsonSerializer.Serialize(request);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Assuming there's an unlock endpoint - if not, we might need to implement it
            var response = await _httpClient.PostAsync("api/wallet/unlockBalance", stringContent);

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

    public async Task<(bool Success, string Message)> IncreaseBalanceAsync(
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

            // Assuming there's an unlock endpoint - if not, we might need to implement it
            var response = await _httpClient.PostAsync("api/wallet/increaseBalance", stringContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully increaseBalance {Amount} {Asset} for user {UserId}",
                    amount, asset, userId);
                return (true, "موجودی با موفقیت آزاد شد");
            }
            else
            {
                _logger.LogWarning("Failed to increase balance for user {UserId}, asset {Asset}, amount {Amount}. Status: {Status}",
                    userId, asset, amount, response.StatusCode);
                return (false, "خطا در آزاد کردن موجودی");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error increase balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, amount);
            return (false, $"خطا در ارتباط با سرویس کیف پول: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate if user has sufficient balance for order
    /// Whether the user has enough balance for an order.
    /// TODO: this should take the quantity as an argument.
    /// valume = price * amount
    /// </summary>
    public async Task<(bool Success, string Message, bool HasSufficientBalance)> ValidateBalanceAsync(
        Guid userId,
        string asset,
        decimal valume)
    {
        try
        {

            var (balanceSuccess, balanceMessage, balance) = await GetBalanceAsync(userId, asset);

            if (balanceSuccess)
            {
                bool HasSufficientBalance = balance >= valume;
                return (true, "چک کردن موجودی", HasSufficientBalance);
            }
            else
            {
                return (false, "خطا در تجزیه پاسخ سرویس", false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, valume);
            return (false, $"خطا در ارتباط با سرویس کیف پول: {ex.Message}", false);
        }
    }
    /// <summary>
    /// Checks a user's credit and balance before an order is placed.
    /// 
    /// </summary>
    /// <param name="userId">User id in our system.</param>
    /// <param name="symbol">
    /// The trading symbol, which names two assets.
    /// Trading Pair: Base Asset / Quote Asset
    /// </param>
    /// <param name="amount">
    /// The quantity the user intends to buy or sell.
    /// Quantity
    /// </param>
    /// <param name="price">
    /// The price, denominated in the quote currency.
    /// Quote Asset
    /// </param>
    /// <returns>
    /// Success is true when the check itself ran without error.
    /// HasSufficientCreditAndBalanceBase is true when the user has enough credit and balance in the
    /// base asset to place a sell order.
    /// HasSufficientCreditAndBalanceQuote is true when they have enough in the quote asset to place
    /// a buy order.
    /// </returns>
    public async Task<(
                        bool Success,
                        string Message,
                        bool HasSufficientCreditAndBalanceBase,
                        bool HasSufficientCreditAndBalanceQuote
        )> 
        ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price)
    {
        try
        {
            // Read the user's various balances.
            var spotBaseAsset = await GetBalanceAsync(userId, symbol.Split('/')[0]);
            var creditBaseAsset = await GetBalanceAsync(userId, "CREDIT_" + symbol.Split('/')[0]);
            var spotQuoteAsset = await GetBalanceAsync(userId, symbol.Split('/')[1]);
            var creditQuoteAsset = await GetBalanceAsync(userId, "CREDIT_" + symbol.Split('/')[1]);

            var spotBaseAssetBalance = spotBaseAsset.Success ? spotBaseAsset.balance : 0;
            var creditBaseAssetBalance = creditBaseAsset.Success ? creditBaseAsset.balance : 0;
            var spotQuoteAssetBalance = spotQuoteAsset.Success ? spotQuoteAsset.balance : 0;
            var creditQuoteAssetBalance = creditQuoteAsset.Success ? creditQuoteAsset.balance : 0;

            return (
                true,
                "اعتبار و موجودی کاربر بررسی شد",
                (spotBaseAssetBalance + creditBaseAssetBalance) + 
                (creditQuoteAssetBalance / price) >= amount,
                (spotQuoteAssetBalance + creditQuoteAssetBalance) +
                (creditBaseAssetBalance * price) >= amount * price
            );
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرویس کیف پول: {ex.Message}", false, false);
        }
    }

    public async Task<TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>> GetUserWalletsBalanceAsync(Guid userId)
    {
        try
        {

            var response = await _httpClient.GetAsync($"api/wallet/balances/{userId}");
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
            _logger?.LogError(ex, "Error fetching the wallet balances of user {UserId}.", userId);
            return TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در ارتباط با سرور");

        }
    }

    public async Task<(bool Success, string Message, decimal? balance)> GetBalanceAsync(Guid userId, string asset)
    {
        // Input validation
        if (userId == Guid.Empty)
        {
            return (false, "شناسه کاربر نامعتبر است.", null);
        }

        if (string.IsNullOrWhiteSpace(asset))
        {
            return (false, "نوع دارایی مشخص نشده است.", null);
        }

        HttpResponseMessage? response = null;
        string? responseContent = null;

        try
        {
            // Create cancellation token with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Make HTTP request with timeout
            response = await _httpClient.GetAsync($"api/wallet/balance/{userId}/{asset}", cts.Token);

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

                    return (true, "موجودی دریافت شد.", walletDto.Data.Balance);
                }
                catch (JsonException jsonEx)
                {
                    _logger?.LogError(jsonEx, "Unreadable balance payload for user {UserId}, asset {Asset}.", userId, asset);
                    return (false, $"خطا در پردازش اطلاعات دریافتی: پاسخ سرور قابل تفسیر نیست.", null);
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

                return (false, errorMessage, null);
            }
        }
        catch (HttpRequestException httpEx)
        {
            // Network-related errors
            return (false, $"خطا در ارتباط شبکه: {httpEx.Message}", null);
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            // Request timeout
            return (false, "زمان انتظار درخواست به پایان رسید. لطفاً مجدداً تلاش کنید.", null);
        }
        catch (TaskCanceledException)
        {
            // Request was cancelled
            return (false, "درخواست لغو شد.", null);
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled
            return (false, "عملیات لغو شد.", null);
        }
        catch (JsonException jsonEx)
        {
            _logger?.LogError(jsonEx, "Unreadable error payload for user {UserId}, asset {Asset}.", userId, asset);
            // JSON parsing errors
            return (false, "خطا در پردازش اطلاعات دریافتی از سرور.", null);
        }
        catch (ArgumentException argEx)
        {
            // Invalid arguments
            return (false, $"پارامتر نامعتبر: {argEx.Message}", null);
        }
        catch (InvalidOperationException invOpEx)
        {
            // Invalid operation state
            return (false, $"عملیات غیرمجاز: {invOpEx.Message}", null);
        }
        catch (Exception ex)
        {
            // Catch-all for any other unexpected exceptions
            return (false, $"خطای غیرمنتظره در ارتباط با سرور: {ex.Message}", null);
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

            var response = await _httpClient.PostAsync($"api/wallet/deposit", content);
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
            _logger?.LogError(ex, "Error depositing to the wallet of user {UserId}.", request.UserId);
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

            var response = await _httpClient.PostAsync($"api/wallet/withdrawal", content);
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
            _logger?.LogError(ex, "Error withdrawing from the wallet of user {UserId}.", request.UserId);
            return TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Fail("خطا در ارتباط با سرور");

        }
    }
    /// <summary>
    /// Once a trade has executed, its transaction must be recorded and the balances updated.
    ///
    /// The reason a settlement was refused must reach the caller verbatim. Every error used to be
    /// collapsed into a generic message, so the outbox processor never learned what actually went
    /// wrong and stored that useless string in LastError (issue #38).
    /// </summary>
    public async Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade)
    {
        try
        {
            var json = JsonSerializer.Serialize(trade);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/wallet/changeBalance", stringContent);
            var body = await response.Content.ReadAsStringAsync();

            // The endpoint returns an ApiResponse on both success and failure, and the precise
            // settlement message is in its Message field.
            var parsed = TryParseMessage(body);

            if (response.IsSuccessStatusCode)
            {
                // _logger is called with ?. deliberately: one of this class's constructors takes no
                // logger and leaves it null.
                _logger?.LogInformation(
                    "Trade {TradeId} settled by the wallet service. Symbol: {Symbol}, quantity: {Quantity}, quote: {QuoteQuantity}.",
                    trade.Id, trade.Symbol, trade.Quantity, trade.QuoteQuantity);

                return (true, parsed ?? "تسویهٔ معامله با موفقیت انجام شد.");
            }

            var reason = parsed ?? $"سرویس کیف پول کد {(int)response.StatusCode} برگرداند.";

            _logger?.LogWarning(
                "Wallet service rejected settlement of trade {TradeId} with status {StatusCode}: {Reason}",
                trade.Id, (int)response.StatusCode, reason);

            return (false, reason);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error settling trade {TradeId} against the wallet service.", trade.Id);
            return (false, $"خطا در ارتباط با سرویس کیف پول: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the message from an ApiResponse body. Returns null when the body is empty or cannot
    /// be parsed, so the caller can supply its own fallback — reading a message must never be what
    /// fails a settlement.
    /// </summary>
    private string? TryParseMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            var apiResponse = JsonSerializer.Deserialize<TallaEgg.Core.DTOs.ApiResponse<string>>(
                body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return string.IsNullOrWhiteSpace(apiResponse?.Message) ? null : apiResponse.Message;
        }
        catch (JsonException)
        {
            _logger?.LogDebug("Wallet settlement response was not a parsable ApiResponse: {Body}", body);
            return null;
        }
    }

}
