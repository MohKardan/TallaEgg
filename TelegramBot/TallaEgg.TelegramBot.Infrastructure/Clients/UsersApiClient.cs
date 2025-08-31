using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// private readonly string _apiUrl;
    /// </summary>
    private readonly string _baseUrl;
    
    public UsersApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["UsersApiUrl"] ?? "http://localhost:5001/api";
        
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
        var uri = $"{_baseUrl}/users/list?pageNumber={pageNumber}&pageSize={pageSize}&q={q}";

        try
        {
            var response = await _httpClient.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? JsonConvert.DeserializeObject<ApiResponse<PagedResult<UserDto>>>(json)
                : ApiResponse<PagedResult<UserDto>>.Fail("دریافت کاربران ناموفق بود");
        }
        catch (Exception ex)
        {
            // TODO: لاگ
            return ApiResponse<PagedResult<UserDto>>.Fail($"خطای ارتباط: {ex.Message}");
        }
    }


    public async Task<(bool isValid, string message)> ValidateInvitationCodeAsync(string invitationCode)
    {
        try
        {
            var request = new { InvitationCode = invitationCode };
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/user/validate-invitation", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<ValidateInvitationResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return (result?.IsValid ?? false, result?.Message ?? "خطا در بررسی کد دعوت");
            }

            return (false, "خطا در ارتباط با سرور");
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return (false, $"خطا در ارتباط: {ex.Message}");
        }
    }
    public async Task<(bool success, string message, Guid? userId)> RegisterUserAsync(long telegramId, string invitationCode, string? username, string? firstName, string? lastName)
    {
        RegisterUserRequest request = new RegisterUserRequest()
        {
            TelegramId = telegramId,
            InvitationCode = invitationCode,
            Username = username,
            FirstName = firstName,
            LastName = lastName
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/user/register", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = Newtonsoft.Json.JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(respText);
                if (result.Success)
                {
                    return (true, "ثبت‌نام با موفقیت انجام شد.", Guid.Parse(result.Data.Id.ToString()));
                }
                return (false, result.Message.ToString(), null);
            }
            return (false, $"خطا در ثبت‌نام: {respText}", null);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرور: {ex.Message}", null);
        }
    }

    public async Task<UserDto?> GetUserAsync(long telegramId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/user/{telegramId}");
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var res = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(respText);
                return res.Data;
            }
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<UserDto?> GetUserAsync(string phone)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/userByPhone/{phone}");
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var res = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(respText);
                return res.Data;
            }
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
    public async Task<TallaEgg.Core.DTOs.ApiResponse<UserDto>> UpdatePhoneAsync(long telegramId, string phoneNumber)
    {
        UpdatePhoneRequest request = new UpdatePhoneRequest()
        {
            TelegramId = telegramId,
            PhoneNumber = phoneNumber
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/user/update-phone", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(respText);
                return result;
            }
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("خطا در ثبت شماره تلفن");
        }
        catch (Exception ex)
        {
            // todo باید اکسپشن برای دولوپر فرستاده بشه
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"خطا در ارتباط با سرور: {ex.Message}");

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
            // PUT: /user/status
            var response = await _httpClient.PutAsync($"{_baseUrl}/user/status", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(respText);
                return result;
            }

            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail("خطا در به‌روزرسانی وضعیت کاربر");
        }
        catch (Exception ex)
        {
            // TODO: ارسال اکسپشن به لاگ یا سرویس مانیتورینگ
            return TallaEgg.Core.DTOs.ApiResponse<UserDto>.Fail($"خطا در ارتباط با سرور: {ex.Message}");
        }
    }


    public async Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/user/getUserIdByInvitationCode/{invitationCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Guid>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return null;
        }
    }

    public async Task<Guid?> GetUserIdByPhoneNumberAsync(string phonenumber)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/user/getUserIdByPhoneNumber/{phonenumber}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Guid>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log exception here if needed
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