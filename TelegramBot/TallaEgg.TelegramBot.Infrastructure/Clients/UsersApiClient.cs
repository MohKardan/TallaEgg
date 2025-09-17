using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Requests.User;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public class UsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<UsersApiClient> _logger;

    public UsersApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<UsersApiClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["UsersApiUrl"] ?? "http://localhost:5001/api";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // برای حل مشکل SSL در محیط توسعه
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", APIKeyConstant.TallaEggApiKey);
    }

    public async Task<ApiResponse<PagedResult<UserDto>>> GetUsersAsync(
        int pageNumber = 1,
        int pageSize = 10,
        string? q = null)
    {
        if (pageNumber <= 0 || pageSize <= 0)
        {
            return ApiResponse<PagedResult<UserDto>>.Fail("پارامترهای صفحه باید بزرگ‌تر از صفر باشند.");
        }

        var uri = $"{_baseUrl}/users/list?pageNumber={pageNumber}&pageSize={pageSize}&q={q}";

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} for GetUsersAsync (page {PageNumber}, size {PageSize}, query {Query}). Payload: {Payload}",
                    (int)response.StatusCode, pageNumber, pageSize, q ?? "-", payload);

                return ApiResponse<PagedResult<UserDto>>.Fail("دریافت کاربران ناموفق بود");
            }

            var result = JsonConvert.DeserializeObject<ApiResponse<PagedResult<UserDto>>>(payload);
            if (result is null)
            {
                _logger.LogError("Users API returned invalid payload for GetUsersAsync. Payload: {Payload}", payload);
                return ApiResponse<PagedResult<UserDto>>.Fail("پاسخ نامعتبر از سرویس کاربران دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while fetching users");
            return ApiResponse<PagedResult<UserDto>>.Fail($"پاسخ‌گویی سرویس کاربران زمان‌بر شد: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while fetching users");
            return ApiResponse<PagedResult<UserDto>>.Fail($"خطای ارتباط با سرویس کاربران: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while fetching users");
            return ApiResponse<PagedResult<UserDto>>.Fail($"ساختار پاسخ سرویس کاربران نامعتبر است: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching users");
            return ApiResponse<PagedResult<UserDto>>.Fail($"خطای غیرمنتظره: {ex.Message}");
        }
    }
    public async Task<(bool isValid, string message)> ValidateInvitationCodeAsync(string invitationCode)
    {
        try
        {
            var request = new { InvitationCode = invitationCode };
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync($"{_baseUrl}/user/validate-invitation", content);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while validating invitation code {InvitationCode}. Payload: {Payload}",
                    (int)response.StatusCode, invitationCode, payload);

                return (false, $"خطا در اعتبارسنجی کد دعوت: {payload}");
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<ValidateInvitationResponse>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result is null)
            {
                _logger.LogError("Users API returned invalid payload while validating invitation code {InvitationCode}. Payload: {Payload}", invitationCode, payload);
                return (false, "پاسخ نامعتبر از سرویس کاربران دریافت شد.");
            }

            return (result.IsValid, result.Message ?? "بررسی کد دعوت انجام شد.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"پاسخ‌گویی سرویس کاربران زمان‌بر شد: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"خطای ارتباط با سرویس کاربران: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"ساختار پاسخ سرویس کاربران نامعتبر است: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while validating invitation code {InvitationCode}", invitationCode);
            return (false, $"خطای غیرمنتظره: {ex.Message}");
        }
    }
    public async Task<(bool success, string message, Guid? userId)> RegisterUserAsync(long telegramId, string invitationCode, string? username, string? firstName, string? lastName)
    {
        var request = new RegisterUserRequest
        {
            TelegramId = telegramId,
            InvitationCode = invitationCode,
            Username = username,
            FirstName = firstName,
            LastName = lastName
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync($"{_baseUrl}/user/register", content);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while registering user {TelegramId}. Payload: {Payload}",
                    (int)response.StatusCode, telegramId, payload);

                return (false, $"خطا در ثبت کاربر: {payload}", null);
            }

            var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(payload);
            if (result?.Success == true && result.Data != null)
            {
                var userId = Guid.TryParse(result.Data.Id.ToString(), out var parsed) ? parsed : (Guid?)null;
                return (true, result.Message ?? "کاربر با موفقیت ثبت شد.", userId);
            }

            if (result != null)
            {
                _logger.LogWarning("Users API reported failure while registering user {TelegramId}: {Message}", telegramId, result.Message);
                return (false, result.Message ?? "ثبت کاربر ناموفق بود.", null);
            }

            _logger.LogError("Users API returned invalid payload while registering user {TelegramId}. Payload: {Payload}", telegramId, payload);
            return (false, "پاسخ نامعتبر از سرویس کاربران دریافت شد.", null);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while registering user {TelegramId}", telegramId);
            return (false, $"پاسخ‌گویی سرویس کاربران زمان‌بر شد: {ex.Message}", null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while registering user {TelegramId}", telegramId);
            return (false, $"خطای ارتباط با سرویس کاربران: {ex.Message}", null);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while registering user {TelegramId}", telegramId);
            return (false, $"ساختار پاسخ سرویس کاربران نامعتبر است: {ex.Message}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while registering user {TelegramId}", telegramId);
            return (false, $"خطای غیرمنتظره: {ex.Message}", null);
        }
    }
    public async Task<UserDto?> GetUserAsync(long telegramId)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/user/{telegramId}");
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while fetching user by TelegramId {TelegramId}. Payload: {Payload}",
                    (int)response.StatusCode, telegramId, payload);
                return null;
            }

            var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(payload);
            if (result?.Data == null)
            {
                _logger.LogWarning("Users API returned empty data while fetching user by TelegramId {TelegramId}. Payload: {Payload}", telegramId, payload);
            }
            return result?.Data;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while fetching user by TelegramId {TelegramId}", telegramId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while fetching user by TelegramId {TelegramId}", telegramId);
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while fetching user by TelegramId {TelegramId}", telegramId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching user by TelegramId {TelegramId}", telegramId);
            return null;
        }
    }
    public async Task<UserDto?> GetUserAsync(string phone)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/userByPhone/{phone}");
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while fetching user by phone {Phone}. Payload: {Payload}",
                    (int)response.StatusCode, phone, payload);
                return null;
            }

            var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(payload);
            if (result?.Data == null)
            {
                _logger.LogWarning("Users API returned empty data while fetching user by phone {Phone}. Payload: {Payload}", phone, payload);
            }
            return result?.Data;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while fetching user by phone {Phone}", phone);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while fetching user by phone {Phone}", phone);
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while fetching user by phone {Phone}", phone);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching user by phone {Phone}", phone);
            return null;
        }
    }
    public async Task<TallaEgg.Core.DTOs.ApiResponse<UserDto>> UpdatePhoneAsync(long telegramId, string phoneNumber)
    {
        var request = new UpdatePhoneRequest
        {
            TelegramId = telegramId,
            PhoneNumber = phoneNumber
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync($"{_baseUrl}/user/update-phone", content);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while updating phone for TelegramId {TelegramId}. Payload: {Payload}",
                    (int)response.StatusCode, telegramId, payload);
                return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("بروزرسانی شماره تلفن ناموفق بود.");
            }

            var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(payload);
            if (result is null)
            {
                _logger.LogError("Users API returned invalid payload while updating phone for TelegramId {TelegramId}. Payload: {Payload}", telegramId, payload);
                return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("پاسخ نامعتبر از سرویس کاربران دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while updating phone for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"پاسخ‌گویی سرویس کاربران زمان‌بر شد: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while updating phone for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"خطای ارتباط با سرویس کاربران: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while updating phone for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"ساختار پاسخ سرویس کاربران نامعتبر است: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating phone for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"خطای غیرمنتظره: {ex.Message}");
        }
    }
    public async Task<TallaEgg.Core.DTOs.ApiResponse<UserDto>> UpdateUserStatusAsync(long telegramId, TallaEgg.Core.Enums.User.UserStatus newStatus)
    {
        var request = new UpdateUserStatusRequest
        {
            TelegramId = telegramId,
            NewStatus = newStatus
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PutAsync($"{_baseUrl}/user/status", content);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while updating user status for TelegramId {TelegramId}. Payload: {Payload}",
                    (int)response.StatusCode, telegramId, payload);
                return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("بروزرسانی وضعیت کاربر ناموفق بود.");
            }

            var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(payload);
            if (result is null)
            {
                _logger.LogError("Users API returned invalid payload while updating user status for TelegramId {TelegramId}. Payload: {Payload}", telegramId, payload);
                return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("پاسخ نامعتبر از سرویس کاربران دریافت شد.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while updating user status for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"پاسخ‌گویی سرویس کاربران زمان‌بر شد: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while updating user status for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"خطای ارتباط با سرویس کاربران: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while updating user status for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"ساختار پاسخ سرویس کاربران نامعتبر است: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating user status for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"خطای غیرمنتظره: {ex.Message}");
        }
    }
    public async Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/user/getUserIdByInvitationCode/{invitationCode}");
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while fetching user id by invitation code {InvitationCode}. Payload: {Payload}",
                    (int)response.StatusCode, invitationCode, payload);
                return null;
            }

            return JsonSerializer.Deserialize<Guid>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while fetching user id by invitation code {InvitationCode}", invitationCode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while fetching user id by invitation code {InvitationCode}", invitationCode);
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while fetching user id by invitation code {InvitationCode}", invitationCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching user id by invitation code {InvitationCode}", invitationCode);
            return null;
        }
    }
    public async Task<Guid?> GetUserIdByPhoneNumberAsync(string phonenumber)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/user/getUserIdByPhoneNumber/{phonenumber}");
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while fetching user id by phone {Phone}. Payload: {Payload}",
                    (int)response.StatusCode, phonenumber, payload);
                return null;
            }

            return JsonSerializer.Deserialize<Guid>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while fetching user id by phone {Phone}", phonenumber);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while fetching user id by phone {Phone}", phonenumber);
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while fetching user id by phone {Phone}", phonenumber);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching user id by phone {Phone}", phonenumber);
            return null;
        }
    }


    /// <summary>
    /// دریافت اطلاعات کاربر بر اساس شناسه کاربر
    /// </summary>
    /// <param name="userId">شناسه یکتای کاربر</param>
    /// <returns>اطلاعات کاربر یا null در صورت عدم وجود</returns>
    /// <remarks>
    /// این متد برای دریافت اطلاعات کامل کاربر شامل TelegramUserId استفاده می‌شود
    /// که برای ارسال اطلاعیه‌های تطبیق معامله ضروری است
    /// </remarks>
    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/user/userId/{userId}");
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while fetching user by id {UserId}. Payload: {Payload}",
                    (int)response.StatusCode, userId, payload);
                return null;
            }

            var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(payload);
            if (result?.Data == null)
            {
                _logger.LogWarning("Users API returned empty data while fetching user by id {UserId}. Payload: {Payload}", userId, payload);
            }
            return result?.Data;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while fetching user by id {UserId}", userId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while fetching user by id {UserId}", userId);
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while fetching user by id {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching user by id {UserId}", userId);
            return null;
        }
    }


    private class ValidateInvitationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = "";
    }

    private class RegisterUserResponse
    {
        public bool Success { get; set; }
        public Guid? UserId { get; set; }
    }
}