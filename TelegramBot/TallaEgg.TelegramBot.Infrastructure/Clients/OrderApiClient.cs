using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orders.Core;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<OrderApiClient> _logger;

    public OrderApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<OrderApiClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["OrderApiUrl"] ?? "http://localhost:5135/api";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var handler = new HttpClientHandler();
#if DEBUG
        // DEV ONLY: accept self-signed certs for local inter-service calls.
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", APIKeyConstant.TallaEggApiKey);
    }

    public async Task<ApiResponse<PagedResult<OrderHistoryDto>>> GetUserOrdersAsync(
        Guid userId,
        int pageNumber = 1,
        int pageSize = 10)
    {
        if (userId == Guid.Empty)
        {
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("شناسه کاربر نامعتبر است.");
        }

        if (pageNumber <= 0 || pageSize <= 0)
        {
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("پارامترهای صفحه باید بزرگ‌تر از صفر باشند.");
        }

        var uri = $"{_baseUrl}/orders/user/{userId}?pageNumber={pageNumber}&pageSize={pageSize}";

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Order API returned {StatusCode} for user {UserId} (page {PageNumber}, size {PageSize}). Payload: {Payload}",
                    (int)response.StatusCode, userId, pageNumber, pageSize, payload);

                var message = $"دریافت سفارشات ناموفق بود (کد {(int)response.StatusCode}).";
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    message += $" جزئیات: {payload}";
                }

                return ApiResponse<PagedResult<OrderHistoryDto>>.Fail(message);
            }

            var result = JsonConvert.DeserializeObject<ApiResponse<PagedResult<OrderHistoryDto>>>(payload);
            if (result is null)
            {
                _logger.LogError("Order API returned an invalid payload for user {UserId}. Payload: {Payload}", userId, payload);
                return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("پاسخ نامعتبر از سرویس سفارشات دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Order API request timed out for user {UserId}", userId);
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("پاسخ‌گویی سرویس سفارشات زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Order API communication error for user {UserId}", userId);
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("خطای ارتباط با سرویس سفارشات");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Order API returned invalid JSON for user {UserId}", userId);
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("ساختار پاسخ سرویس سفارشات نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching orders for user {UserId}", userId);
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("خطای غیرمنتظره");
        }
    }
    public async Task<ApiResponse<PagedResult<TradeHistoryDto>>> GetUserTradesAsync(
        Guid userId,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var uri = $"{_baseUrl}/trades/user/{userId}?pageNumber={pageNumber}&pageSize={pageSize}";

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Order API returned {StatusCode} for user {UserId} trades (page {PageNumber}, size {PageSize}). Payload: {Payload}",
                    (int)response.StatusCode, userId, pageNumber, pageSize, payload);

                return ApiResponse<PagedResult<TradeHistoryDto>>.Fail("دریافت معاملات ناموفق بود");
            }

            var result = JsonConvert.DeserializeObject<ApiResponse<PagedResult<TradeHistoryDto>>>(payload);
            if (result is null)
            {
                _logger.LogError("Order API returned an invalid trades payload for user {UserId}. Payload: {Payload}", userId, payload);
                return ApiResponse<PagedResult<TradeHistoryDto>>.Fail("پاسخ نامعتبر از سرویس سفارشات دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Order API request timed out while fetching trades for user {UserId}", userId);
            return ApiResponse<PagedResult<TradeHistoryDto>>.Fail("پاسخ‌گویی سرویس سفارشات زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Order API communication error while fetching trades for user {UserId}", userId);
            return ApiResponse<PagedResult<TradeHistoryDto>>.Fail("خطای ارتباط با سرویس سفارشات");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Order API returned invalid JSON while fetching trades for user {UserId}", userId);
            return ApiResponse<PagedResult<TradeHistoryDto>>.Fail("ساختار پاسخ سرویس سفارشات نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching trades for user {UserId}", userId);
            return ApiResponse<PagedResult<TradeHistoryDto>>.Fail("خطای غیرمنتظره");
        }
    }
    public async Task<ApiResponse<List<OrderHistoryDto>>> GetUserActiveOrdersAsync(Guid userId)
    {
        var uri = $"{_baseUrl}/orders/active/user/{userId}";

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Order API returned {StatusCode} while fetching active orders for user {UserId}. Payload: {Payload}",
                    (int)response.StatusCode, userId, payload);

                return ApiResponse<List<OrderHistoryDto>>.Fail("دریافت سفارشات فعال ناموفق بود");
            }

            var result = JsonConvert.DeserializeObject<ApiResponse<List<OrderHistoryDto>>>(payload);
            if (result is null)
            {
                _logger.LogError("Order API returned an invalid active orders payload for user {UserId}. Payload: {Payload}", userId, payload);
                return ApiResponse<List<OrderHistoryDto>>.Fail("پاسخ نامعتبر از سرویس سفارشات دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Order API request timed out while fetching active orders for user {UserId}", userId);
            return ApiResponse<List<OrderHistoryDto>>.Fail("پاسخ‌گویی سرویس سفارشات زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Order API communication error while fetching active orders for user {UserId}", userId);
            return ApiResponse<List<OrderHistoryDto>>.Fail("خطای ارتباط با سرویس سفارشات");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Order API returned invalid JSON while fetching active orders for user {UserId}", userId);
            return ApiResponse<List<OrderHistoryDto>>.Fail("ساختار پاسخ سرویس سفارشات نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching active orders for user {UserId}", userId);
            return ApiResponse<List<OrderHistoryDto>>.Fail("خطای غیرمنتظره");
        }
    }
    public async Task<ApiResponse<List<OrderHistoryDto>>> GetAllActiveOrdersAsync()
    {
        var uri = $"{_baseUrl}/orders/active/all";

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Order API returned {StatusCode} while fetching all active orders. Payload: {Payload}", (int)response.StatusCode, payload);
                return ApiResponse<List<OrderHistoryDto>>.Fail("دریافت تمام سفارشات فعال ناموفق بود");
            }

            var result = JsonConvert.DeserializeObject<ApiResponse<List<OrderHistoryDto>>>(payload);
            if (result is null)
            {
                _logger.LogError("Order API returned an invalid payload while fetching all active orders. Payload: {Payload}", payload);
                return ApiResponse<List<OrderHistoryDto>>.Fail("پاسخ نامعتبر از سرویس سفارشات دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Order API request timed out while fetching all active orders");
            return ApiResponse<List<OrderHistoryDto>>.Fail("پاسخ‌گویی سرویس سفارشات زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Order API communication error while fetching all active orders");
            return ApiResponse<List<OrderHistoryDto>>.Fail("خطای ارتباط با سرویس سفارشات");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Order API returned invalid JSON while fetching all active orders");
            return ApiResponse<List<OrderHistoryDto>>.Fail("ساختار پاسخ سرویس سفارشات نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching all active orders");
            return ApiResponse<List<OrderHistoryDto>>.Fail("خطای غیرمنتظره");
        }
    }


    // ...existing code...

    public async Task<TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>> GetBestPricesAsync(string symbol)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("نماد ارز مشخص نشده است.");
        }

        HttpResponseMessage? response = null;
        string? responseContent = null;

        try
        {
            // Normalize symbol and try to split into base/quote
            var normalized = symbol.Trim().ToUpperInvariant();
            string baseAsset = string.Empty;
            string quoteAsset = string.Empty;

            if (normalized.Contains('/'))
            {
                var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    baseAsset = parts[0];
                    quoteAsset = parts[1];
                }
            }
            else if (normalized.Contains('-'))
            {
                var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    baseAsset = parts[0];
                    quoteAsset = parts[1];
                }
            }

            // If we have both assets, call the Base/Quote route; otherwise fallback to single-segment (URI-escaped)
            string requestUri;
            if (!string.IsNullOrWhiteSpace(baseAsset) && !string.IsNullOrWhiteSpace(quoteAsset))
            {
                requestUri = $"{_baseUrl}/orders/{baseAsset}/{quoteAsset}/best-prices";
            }
            else
            {
                // Use Uri.EscapeDataString so symbols with special chars are safely encoded
                var encoded = Uri.EscapeDataString(normalized);
                requestUri = $"{_baseUrl}/orders/{encoded}/best-prices";
            }

            // Create cancellation token with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Make HTTP request with timeout
            response = await _httpClient.GetAsync(requestUri, cts.Token);

            // Read response content
            responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Handle successful response
                try
                {
                    var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>>(responseContent);
                    return result ?? TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("پاسخ سرور خالی است.");
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("خطا در پردازش اطلاعات دریافتی: پاسخ سرور قابل تفسیر نیست.");
                }
            }
            else
            {
                // Handle HTTP error status codes
                var errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "نماد مورد نظر یافت نشد یا بازار برای این نماد وجود ندارد.",
                    System.Net.HttpStatusCode.Unauthorized => "عدم دسترسی: احراز هویت نشده است.",
                    System.Net.HttpStatusCode.Forbidden => "عدم دسترسی: دسترسی به این عملیات مجاز نیست.",
                    System.Net.HttpStatusCode.BadRequest => "درخواست نامعتبر: نماد ارسالی صحیح نیست.",
                    System.Net.HttpStatusCode.InternalServerError => "خطای داخلی سرور.",
                    System.Net.HttpStatusCode.ServiceUnavailable => "سرویس قیمت‌گذاری در دسترس نیست.",
                    System.Net.HttpStatusCode.RequestTimeout => "زمان انتظار درخواست به پایان رسید.",
                    System.Net.HttpStatusCode.TooManyRequests => "تعداد درخواست‌های زیاد. لطفاً کمی صبر کنید.",
                    _ => $"خطا در دریافت قیمت‌ها: کد خطا {(int)response.StatusCode}"
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

                return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail(errorMessage);
            }
        }
        catch (HttpRequestException)
        {
            // Network-related errors
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("خطا در ارتباط شبکه. لطفاً اتصال اینترنت خود را بررسی کنید.");
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            // Request timeout
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("زمان انتظار درخواست به پایان رسید. لطفاً مجدداً تلاش کنید.");
        }
        catch (TaskCanceledException)
        {
            // Request was cancelled
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("درخواست لغو شد.");
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("عملیات لغو شد.");
        }
        catch (ArgumentException)
        {
            // Invalid arguments
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("پارامتر ورودی نامعتبر است.");
        }
        catch (InvalidOperationException)
        {
            // Invalid operation state
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("عملیات در وضعیت فعلی مجاز نیست.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching the best prices.");
            // Catch-all for any other unexpected exceptions
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail("خطای غیرمنتظره");
        }
        finally
        {
            // Cleanup resources if needed
            response?.Dispose();
        }
    }

    public async Task<(bool success, string message)> SubmitOrderAsync(OrderDto order)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(order);
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
            _logger.LogError(ex, "Unexpected error while submitting an order.");
            return (false, "خطا در ارتباط با سرور");
        }
    }

    /// <summary>
    /// Publishes the admin's quote. Prices are sent per base unit, toman per gram; the conversion
    /// from mesghal happens before this method is called.
    /// </summary>
    public async Task<(bool success, string message)> PublishQuoteAsync(
        string symbol, decimal buyPrice, decimal sellPrice, Guid publishedByUserId)
    {
        try
        {
            var payload = new { symbol, buyPrice, sellPrice, publishedByUserId };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/quotes", content);
            var body = await response.Content.ReadAsStringAsync();

            // The server's own message is returned rather than replaced with generic text — the
            // reason for a refusal, a negative spread for instance, is useful to the admin. Same
            // lesson as issue #38.
            var message = TryReadMessage(body);

            return response.IsSuccessStatusCode
                ? (true, message ?? "مظنه منتشر شد.")
                : (false, message ?? $"خطا در انتشار مظنه (کد {(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while publishing a quote.");
            return (false, "خطا در ارتباط با سرور");
        }
    }

    // ── Automatic quotes (issue #90) ────────────────────────────────────────────

    public async Task<AutoQuoteSettingsDto?> GetAutoQuoteSettingsAsync(string symbol)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/autoquote-settings/{symbol}");
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var parsed = System.Text.Json.JsonSerializer.Deserialize<TallaEgg.Core.DTOs.ApiResponse<AutoQuoteSettingsDto>>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return parsed?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool success, string message)> UpdateAutoQuoteSpreadAsync(string symbol, decimal spreadPercent, Guid updatedByUserId)
    {
        try
        {
            var payload = new { spreadPercent, updatedByUserId };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/autoquote-settings/{symbol}/spread", content);
            var body = await response.Content.ReadAsStringAsync();
            var message = TryReadMessage(body);

            return response.IsSuccessStatusCode
                ? (true, message ?? "اسپرد به‌روزرسانی شد.")
                : (false, message ?? $"خطا در به‌روزرسانی اسپرد (کد {(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating the auto-quote spread.");
            return (false, "خطا در ارتباط با سرور");
        }
    }

    public async Task<(bool success, string message)> SetAutoQuoteEnabledAsync(string symbol, bool isEnabled, Guid updatedByUserId)
    {
        try
        {
            var payload = new { isEnabled, updatedByUserId };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/autoquote-settings/{symbol}/enabled", content);
            var body = await response.Content.ReadAsStringAsync();
            var message = TryReadMessage(body);

            return response.IsSuccessStatusCode
                ? (true, message ?? "انجام شد.")
                : (false, message ?? $"خطا (کد {(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while switching auto-quote on or off.");
            return (false, "خطا در ارتباط با سرور");
        }
    }

    /// <summary>The symbols currently tradable, or empty if the server is unreachable.</summary>
    public async Task<List<string>> GetActiveSymbolsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/symbols/active");
            if (!response.IsSuccessStatusCode) return new List<string>();

            var body = await response.Content.ReadAsStringAsync();
            var parsed = System.Text.Json.JsonSerializer.Deserialize<TallaEgg.Core.DTOs.ApiResponse<List<string>>>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return parsed?.Data ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<(bool success, string message)> SetSymbolActiveAsync(string symbol, bool isActive, Guid updatedByUserId)
    {
        try
        {
            var payload = new { isActive, updatedByUserId };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/symbols/{symbol}/active", content);
            var body = await response.Content.ReadAsStringAsync();
            var message = TryReadMessage(body);

            return response.IsSuccessStatusCode
                ? (true, message ?? "انجام شد.")
                : (false, message ?? $"خطا (کد {(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while switching a symbol on or off.");
            return (false, "خطا در ارتباط با سرور");
        }
    }

    /// <summary>The active quote for a symbol, or null if none has been published.</summary>
    public async Task<QuoteDto?> GetActiveQuoteAsync(string symbol)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/quotes/{symbol}");
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var parsed = System.Text.Json.JsonSerializer.Deserialize<TallaEgg.Core.DTOs.ApiResponse<QuoteDto>>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return parsed?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Published quotes for a symbol, newest first, including replaced ones.
    ///
    /// Returns an empty page rather than null on failure, so the caller renders "no quotes
    /// yet" instead of having to distinguish a network error from an empty history. The
    /// distinction matters to an operator reading logs, not to the customer reading a list.
    /// </summary>
    public async Task<PagedResult<QuoteDto>> GetQuoteHistoryAsync(string symbol, int pageNumber = 1, int pageSize = 5)
    {
        var empty = new PagedResult<QuoteDto>
        {
            Items = new List<QuoteDto>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/quotes/{symbol}/history?page={pageNumber}&size={pageSize}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Quote history request for {Symbol} returned {Status}.", symbol, response.StatusCode);
                return empty;
            }

            var body = await response.Content.ReadAsStringAsync();
            var parsed = System.Text.Json.JsonSerializer.Deserialize<TallaEgg.Core.DTOs.ApiResponse<PagedResult<QuoteDto>>>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return parsed?.Data ?? empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read quote history for {Symbol}.", symbol);
            return empty;
        }
    }

    /// <summary>
    /// A customer fills a quote. No price is sent — the server reads it from the published quote.
    /// </summary>
    public async Task<(bool success, string message)> AcceptQuoteAsync(
        Guid userId, string symbol, OrderSide side, decimal quantity)
    {
        try
        {
            var payload = new { userId, symbol, side, quantity };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/quotes/accept", content);
            var body = await response.Content.ReadAsStringAsync();
            var message = TryReadMessage(body);

            return response.IsSuccessStatusCode
                ? (true, message ?? "معامله انجام شد.")
                : (false, message ?? "انجام معامله ممکن نشد.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while accepting a quote.");
            return (false, "خطا در ارتباط با سرور");
        }
    }

    /// <summary>
    /// Extracts the message from an ApiResponse body, returning null when it cannot be parsed so the
    /// caller can supply its own fallback — reading a message must never fail the operation.
    /// </summary>
    private static string? TryReadMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<TallaEgg.Core.DTOs.ApiResponse<object>>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return string.IsNullOrWhiteSpace(parsed?.Message) ? null : parsed.Message;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public async Task<(bool success, string message)> CancelOrderAsync(Guid orderId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/orders/{orderId}/cancel", null);
            var respText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return (true, "سفارش شما لغو شد.");
            return (false, $"خطا در لغو سفارش: {respText}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while cancelling an order.");
            return (false, "خطا در ارتباط با سرور");
        }
    }

    /// <summary>
    /// Cancels all of a user's active orders through the API.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    /// <returns>
    /// A tuple of: success, whether the operation succeeded; message, the server's explanation; and
    /// cancelledCount, how many orders were cancelled.
    /// </returns>
    /// <remarks>
    /// POSTs to the cancel-orders endpoint with the reason in the body, parses the ApiResponse,
    /// extracts the cancelled count, and turns any failure into a usable message.
    /// </remarks>
    public async Task<(bool success, string message, int cancelledCount)> CancelAllUserActiveOrdersAsync(Guid userId, string? reason = null)
    {
        try
        {
            var requestBody = new { reason };
            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/orders/user/{userId}/cancel-active", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<CancelActiveOrdersResponseDto>>(respText);
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    return (true, apiResponse.Message ?? "سفارشات لغو شدند", apiResponse.Data.CancelledCount);
                }
                return (false, apiResponse?.Message ?? "خطا در پردازش پاسخ", 0);
            }

            var errorResponse = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<object>>(respText);
            return (false, errorResponse?.Message ?? $"خطا در لغو سفارشات: {respText}", 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while cancelling a user's active orders.");
            return (false, "خطا در ارتباط با سرور", 0);
        }
    }

    public async Task<ApiResponse<bool>> NotifyMatchingEngineAsync(NotifyMatchingEngineRequest request)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/orders/market/notify-matching", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<bool>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result ?? ApiResponse<bool>.Fail("خطا در پردازش پاسخ");
            }

            return ApiResponse<bool>.Fail($"خطا در اطلاع‌رسانی به موتور تطبیق: {responseContent}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while notifying the matching engine.");
            return ApiResponse<bool>.Fail("خطا در ارتباط با سرور");
        }
    }

    public async Task<ApiResponse<PositionsResponseDto>> GetPositionsAsync(Guid userId)
    {
        var uri = $"{_baseUrl}/positions/user/{userId}";

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Order API returned {StatusCode} for user {UserId} positions. Payload: {Payload}",
                    (int)response.StatusCode, userId, payload);

                return ApiResponse<PositionsResponseDto>.Fail("دریافت سود و زیان ناموفق بود");
            }

            var result = JsonConvert.DeserializeObject<ApiResponse<PositionsResponseDto>>(payload);
            if (result is null)
            {
                _logger.LogError("Order API returned an invalid positions payload for user {UserId}. Payload: {Payload}", userId, payload);
                return ApiResponse<PositionsResponseDto>.Fail("پاسخ نامعتبر از سرویس سفارشات دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Order API request timed out while fetching positions for user {UserId}", userId);
            return ApiResponse<PositionsResponseDto>.Fail("پاسخ‌گویی سرویس سفارشات زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Order API communication error while fetching positions for user {UserId}", userId);
            return ApiResponse<PositionsResponseDto>.Fail("خطای ارتباط با سرویس سفارشات");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Order API returned invalid JSON while fetching positions for user {UserId}", userId);
            return ApiResponse<PositionsResponseDto>.Fail("ساختار پاسخ سرویس سفارشات نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching positions for user {UserId}", userId);
            return ApiResponse<PositionsResponseDto>.Fail("خطای غیرمنتظره");
        }
    }
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

public class NotifyMatchingEngineRequest
{
    public Guid OrderId { get; set; }
    public string Asset { get; set; } = "";
    public OrderSide Type { get; set; }
}

public class CancelActiveOrdersResponseDto
{
    public int CancelledCount { get; set; }
}