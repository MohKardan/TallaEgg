using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Requests.User;
using TallaEgg.TelegramBot.Core.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public class UsersApiClient : IUsersApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// private readonly string _apiUrl;
    /// </summary>
    private readonly string _baseUrl;
    
    public UsersApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["UsersApiUrl"] ?? "https://localhost:7296/api";
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

    public async Task<User?> RegisterUserAsync_0(long telegramId, string? username, string? firstName, string? lastName, string invitationCode)
    {
        try
        {
            var request = new
            {
                TelegramId = telegramId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                InvitationCode = invitationCode,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/user/register", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<RegisterUserResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Success == true)
                {
                    return await GetUserByTelegramIdAsync(telegramId);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return null;
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

    public async Task<User?> GetUserByTelegramIdAsync(long telegramId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/user/{telegramId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<User>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return null;
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
    public async Task<User?> UpdateUserPhoneAsync(long telegramId, string phoneNumber)
    {
        try
        {
            var request = new
            {
                TelegramId = telegramId,
                PhoneNumber = phoneNumber
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/user/update-phone", content);

            if (response.IsSuccessStatusCode)
            {
                return await GetUserByTelegramIdAsync(telegramId);
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return null;
        }
    }
    public async Task<(bool success, string message)> UpdatePhoneAsync(long telegramId, string phoneNumber)
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
                return (result.Success, result.Message.ToString());
            }
            return (false, $"خطا در ثبت شماره تلفن: {respText}");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
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

    public async Task<User?> RegisterUserAsync(User user)
    {
        try
        {
            var json = JsonSerializer.Serialize(user);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/user/register", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RegisterUserResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Success == true)
                {
                    return user;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return null;
        }
    }

    Task<User?> IUsersApiClient.RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode)
    {
        throw new NotImplementedException();
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