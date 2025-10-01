using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core;

namespace TallaEgg.TelegramBot;

public class AffiliateApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AffiliateApiClient> _logger;

    public AffiliateApiClient(string apiUrl, HttpClient httpClient, ILogger<AffiliateApiClient> logger)
    {
        _apiUrl = apiUrl;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // برای حل مشکل SSL در محیط توسعه
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", APIKeyConstant.TallaEggApiKey);
    }
    public async Task<(bool success, string message)> ValidateInvitationAsync(string invitationCode)
    {
        var request = new { InvitationCode = invitationCode };
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/validate-invitation", content);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Affiliate API returned {StatusCode} while validating invitation code {InvitationCode}. Payload: {Payload}",
                    (int)response.StatusCode, invitationCode, payload);
                return (false, $"خطا در اعتبارسنجی کد دعوت: {payload}");
            }

            var result = JsonConvert.DeserializeObject<ValidateInvitationResponse>(payload);
            if (result is null)
            {
                _logger.LogError("Affiliate API returned invalid payload while validating invitation code {InvitationCode}. Payload: {Payload}", invitationCode, payload);
                return (false, "پاسخ نامعتبر از سرویس افیلیت دریافت شد.");
            }

            return (result.IsValid, result.Message ?? "نتیجه بررسی کد دعوت دریافت شد.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Affiliate API request timed out while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"پاسخ‌گویی سرویس افیلیت زمان‌بر شد: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Affiliate API communication error while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"خطای ارتباط با سرویس افیلیت: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Affiliate API returned invalid JSON while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"ساختار پاسخ سرویس افیلیت نامعتبر است: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"خطای غیرمنتظره: {ex.Message}");
        }
    }
    public async Task<(bool success, string message, Guid? invitationId)> UseInvitationAsync(string invitationCode, Guid usedByUserId)
    {
        var request = new
        {
            InvitationCode = invitationCode,
            UsedByUserId = usedByUserId
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/use-invitation", content);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Affiliate API returned {StatusCode} while consuming invitation code {InvitationCode} for user {UserId}. Payload: {Payload}",
                    (int)response.StatusCode, invitationCode, usedByUserId, payload);
                return (false, $"استفاده از کد دعوت ناموفق بود: {payload}", null);
            }

            var result = JsonConvert.DeserializeObject<UseInvitationResponse>(payload);
            if (result is null)
            {
                _logger.LogError("Affiliate API returned invalid payload while consuming invitation code {InvitationCode}. Payload: {Payload}", invitationCode, payload);
                return (false, "پاسخ نامعتبر از سرویس افیلیت دریافت شد.", null);
            }

            if (result.Success)
            {
                return (true, result.Message ?? "کد دعوت با موفقیت استفاده شد.", result.InvitationId);
            }

            _logger.LogWarning("Affiliate API reported failure while consuming invitation code {InvitationCode}: {Message}", invitationCode, result.Message);
            return (false, result.Message ?? "استفاده از کد دعوت ناموفق بود.", null);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Affiliate API request timed out while consuming invitation code {InvitationCode}", invitationCode);
            return (false, $"پاسخ‌گویی سرویس افیلیت زمان‌بر شد: {ex.Message}", null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Affiliate API communication error while consuming invitation code {InvitationCode}", invitationCode);
            return (false, $"خطای ارتباط با سرویس افیلیت: {ex.Message}", null);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Affiliate API returned invalid JSON while consuming invitation code {InvitationCode}", invitationCode);
            return (false, $"ساختار پاسخ سرویس افیلیت نامعتبر است: {ex.Message}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while consuming invitation code {InvitationCode}", invitationCode);
            return (false, $"خطای غیرمنتظره: {ex.Message}", null);
        }
    }


    public async Task<(bool success, InvitationDto? invitation)> CreateInvitationAsync(Guid createdByUserId, string type = "Regular", int maxUses = -1)
    {
        var request = new
        {
            CreatedByUserId = createdByUserId,
            Type = type,
            MaxUses = maxUses
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/create-invitation", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<CreateInvitationResponse>(respText);
                if (result?.Success == true)
                {
                    return (true, result.Invitation);
                }
                return (false, null);
            }
            return (false, null);
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return (false, null);
        }
    }

    public async Task<(bool success, string message)> UpdateUserPhoneAsync(long telegramId, string phoneNumber)
    {
        var request = new
        {
            TelegramId = telegramId,
            PhoneNumber = phoneNumber
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/update-user-phone", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, "شماره تلفن با موفقیت به‌روزرسانی شد.");
            }
            return (false, $"خطا در به‌روزرسانی شماره تلفن: {respText}");
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    private class ValidateInvitationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = "";
    }

    private class UseInvitationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public Guid? InvitationId { get; set; }
    }

    private class CreateInvitationResponse
    {
        public bool Success { get; set; }
        public InvitationDto? Invitation { get; set; }
    }
}

public class InvitationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
    public string Type { get; set; } = "";
}