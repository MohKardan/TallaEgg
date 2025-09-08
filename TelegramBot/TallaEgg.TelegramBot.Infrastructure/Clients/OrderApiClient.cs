using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Orders.Core;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OrderApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["OrderApiUrl"] ?? "http://localhost:5135/api";
        
        // برای حل مشکل SSL در محیط توسعه
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
    }

    public async Task<ApiResponse<PagedResult<OrderHistoryDto>>> GetUserOrdersAsync(
    Guid userId,
    int pageNumber = 1,
    int pageSize = 10)
    {
        var uri = $"{_baseUrl}/orders/user/{userId}?pageNumber={pageNumber}&pageSize={pageSize}";

        try
        {
            var response = await _httpClient.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? JsonConvert.DeserializeObject<ApiResponse<PagedResult<OrderHistoryDto>>>(json)
                : ApiResponse<PagedResult<OrderHistoryDto>>.Fail("دریافت سفارشات ناموفق بود");
        }
        catch (Exception ex)
        {
            // TODO: لاگ
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail($"خطای ارتباط: {ex.Message}");
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
            // Create cancellation token with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Make HTTP request with timeout
            response = await _httpClient.GetAsync($"{_baseUrl}/orders/{symbol}/best-prices", cts.Token);

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
            // Catch-all for any other unexpected exceptions
            return TallaEgg.Core.DTOs.ApiResponse<BestPricesDto>.Fail($"خطای غیرمنتظره: {ex.Message}");
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
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
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
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    /// <summary>
    /// کنسل کردن تمام سفارشات فعال یک کاربر از طریق API
    /// </summary>
    /// <param name="userId">شناسه کاربر</param>
    /// <param name="reason">دلیل کنسل کردن سفارشات (اختیاری)</param>
    /// <returns>
    /// Tuple شامل:
    /// - success: آیا عملیات موفق بوده یا نه
    /// - message: پیام توضیحی از سرور
    /// - cancelledCount: تعداد سفارشات کنسل شده
    /// </returns>
    /// <remarks>
    /// این تابع:
    /// 1. درخواست POST به endpoint کنسل سفارشات ارسال می‌کند
    /// 2. دلیل کنسل را در بدنه درخواست ارسال می‌کند
    /// 3. پاسخ ApiResponse را پارس می‌کند
    /// 4. تعداد سفارشات کنسل شده را استخراج و برمی‌گرداند
    /// 5. خطاها را handle کرده و پیام مناسب برمی‌گرداند
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
            return (false, $"خطا در ارتباط با سرور: {ex.Message}", 0);
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
            return ApiResponse<bool>.Fail($"خطا در ارتباط با سرور: {ex.Message}");
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