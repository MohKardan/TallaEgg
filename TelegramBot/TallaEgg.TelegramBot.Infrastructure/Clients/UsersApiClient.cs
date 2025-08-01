using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public class UsersApiClient : IUsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public UsersApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["UsersApiUrl"] ?? "http://localhost:5136/api";
    }

    public async Task<(bool isValid, string message)> ValidateInvitationCodeAsync(string invitationCode)
    {
        try
        {
            var request = new { InvitationCode = invitationCode };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/user/validate-invitation", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ValidateInvitationResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return (result?.IsValid ?? false, result?.Message ?? "خطا در بررسی کد دعوت");
            }

            return (false, "خطا در ارتباط با سرور");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط: {ex.Message}");
        }
    }

    public async Task<User?> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode)
    {
        try
        {
            var request = new
            {
                TelegramId = telegramId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                InvitationCode = invitationCode
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/user/register", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RegisterUserResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (result?.Success == true)
                {
                    return await GetUserByTelegramIdAsync(telegramId);
                }
            }

            return null;
        }
        catch (Exception)
        {
            return null;
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
        catch (Exception)
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
        catch (Exception)
        {
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