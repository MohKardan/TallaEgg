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

public class UsersApiClient : IUsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<UsersApiClient> _logger;

    public UsersApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<UsersApiClient> logger)
    {
        _httpClient = httpClient;

        // Read from this host's own section and nowhere else. It used to fall through to a scan
        // of every key in the merged configuration for anything ending in "UsersApiUrl", which
        // meant Orders.Api — whose section defined no such key — was served by whichever other
        // service's section the enumeration reached first. Two of them held incompatible shapes,
        // one with an /api suffix and one without, so the address that worked was landed on
        // rather than chosen (issue #205). Every caller now defines its own key.
        //
        // AbsoluteUri rather than ToString, which unescapes: a percent-escaped or non-ASCII
        // host would come back decoded and be concatenated into a request against a different
        // authority than was configured. TrimEnd because the paths below are concatenated, not
        // resolved: Uri normalises an empty path to "/", which would produce "host//users/list".
        _baseUrl = ConfigurationGuard.RequireUri(configuration, "UsersApiUrl").AbsoluteUri.TrimEnd('/');
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var handler = new HttpClientHandler();
#if DEBUG
        // DEV ONLY: accept self-signed certs for local inter-service calls.
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
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
            return ApiResponse<PagedResult<UserDto>>.Fail("پاسخ‌گویی سرویس کاربران زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while fetching users");
            return ApiResponse<PagedResult<UserDto>>.Fail("خطای ارتباط با سرویس کاربران");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while fetching users");
            return ApiResponse<PagedResult<UserDto>>.Fail("ساختار پاسخ سرویس کاربران نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching users");
            return ApiResponse<PagedResult<UserDto>>.Fail("خطای غیرمنتظره");
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
            return (false, "پاسخ‌گویی سرویس کاربران زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while validating invitation code {InvitationCode}", invitationCode);
            return (false, "خطای ارتباط با سرویس کاربران");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while validating invitation code {InvitationCode}", invitationCode);
            return (false, "ساختار پاسخ سرویس کاربران نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while validating invitation code {InvitationCode}", invitationCode);
            return (false, "خطای غیرمنتظره");
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
            return (false, "پاسخ‌گویی سرویس کاربران زمان‌بر شد", null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while registering user {TelegramId}", telegramId);
            return (false, "خطای ارتباط با سرویس کاربران", null);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while registering user {TelegramId}", telegramId);
            return (false, "ساختار پاسخ سرویس کاربران نامعتبر است", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while registering user {TelegramId}", telegramId);
            return (false, "خطای غیرمنتظره", null);
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
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("پاسخ‌گویی سرویس کاربران زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while updating phone for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("خطای ارتباط با سرویس کاربران");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while updating phone for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("ساختار پاسخ سرویس کاربران نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating phone for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("خطای غیرمنتظره");
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
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("پاسخ‌گویی سرویس کاربران زمان‌بر شد");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while updating user status for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("خطای ارتباط با سرویس کاربران");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Users API returned invalid JSON while updating user status for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("ساختار پاسخ سرویس کاربران نامعتبر است");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating user status for TelegramId {TelegramId}", telegramId);
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("خطای غیرمنتظره");
        }
    }
    /// <summary>
    /// Changes a user's role through <c>POST /api/user/update-role</c>.
    ///
    /// <para>
    /// Returns the service's own message rather than a generic one. A role change is rare and
    /// consequential, so when it fails the operator needs to know whether the user was not
    /// found, the service was unreachable, or the request was rejected — "failed" alone would
    /// leave them guessing at the one moment they cannot afford to.
    /// </para>
    ///
    /// <para>
    /// The endpoint answers <c>{ success, message }</c>, not the usual <c>ApiResponse&lt;T&gt;</c>
    /// envelope, so the payload is read into a matching shape instead.
    /// </para>
    /// </summary>
    public async Task<(bool success, string message)> UpdateRoleAsync(Guid userId, TallaEgg.Core.Enums.User.UserRole newRole)
    {
        var json = JsonConvert.SerializeObject(new { UserId = userId, NewRole = newRole });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync($"{_baseUrl}/user/update-role", content);
            var payload = await response.Content.ReadAsStringAsync();

            var parsed = JsonConvert.DeserializeObject<UpdateRoleResponse>(payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Users API returned {StatusCode} while updating role of {UserId} to {NewRole}. Payload: {Payload}",
                    (int)response.StatusCode, userId, newRole, payload);

                // 404 and 400 both carry a usable message; fall back only when they do not.
                return (false, string.IsNullOrWhiteSpace(parsed?.Message)
                    ? "تغییر سطح دسترسی ناموفق بود."
                    : parsed!.Message!);
            }

            if (parsed is null)
            {
                _logger.LogError("Users API returned invalid payload while updating role of {UserId}. Payload: {Payload}", userId, payload);
                return (false, "پاسخ نامعتبر از سرویس کاربران دریافت شد.");
            }

            return (parsed.Success, string.IsNullOrWhiteSpace(parsed.Message) ? "انجام شد." : parsed.Message);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Users API request timed out while updating role of {UserId}", userId);
            return (false, "پاسخ‌گویی سرویس کاربران زمان‌بر شد.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Users API communication error while updating role of {UserId}", userId);
            return (false, "خطای ارتباط با سرویس کاربران.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating role of {UserId}", userId);
            return (false, "خطای غیرمنتظره در تغییر سطح دسترسی.");
        }
    }

    /// <summary>The bare shape <c>/api/user/update-role</c> answers with.</summary>
    private sealed class UpdateRoleResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public async Task<List<long>> GetOperatorTelegramIdsAsync()
    {
        var ids = new List<long>();

        foreach (var role in new[] { "Admin", "SuperAdmin" })
        {
            try
            {
                using var response = await _httpClient.GetAsync($"{_baseUrl}/users/by-role/{role}");
                var payload = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Users API returned {StatusCode} while fetching users with role {Role}. Payload: {Payload}",
                        (int)response.StatusCode, role, payload);
                    continue;
                }

                var users = JsonConvert.DeserializeObject<List<OperatorLookupUser>>(payload);
                if (users is null) continue;

                // TelegramId 0 is the auto-seeded SuperAdmin row (Users.Api/Program.cs) — no
                // human is signed in as it, and sending a Telegram message to chat id 0 would
                // just fail.
                ids.AddRange(users.Where(u => u.TelegramId != 0).Select(u => u.TelegramId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching users with role {Role}", role);
            }
        }

        return ids.Distinct().ToList();
    }

    /// <summary>The bare shape <c>/api/users/by-role/{role}</c> answers with — only what this lookup needs.</summary>
    private sealed class OperatorLookupUser
    {
        public long TelegramId { get; set; }
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
    /// Returns a user by id.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>The user, or null if there is no such user.</returns>
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
