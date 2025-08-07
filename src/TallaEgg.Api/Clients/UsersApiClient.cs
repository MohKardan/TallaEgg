using System.Text;
using System.Text.Json;

namespace TallaEgg.Api.Clients;

public class UsersApiClient : IUsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public UsersApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["UsersApiUrl"] ?? "http://localhost:5136";
    }

    public async Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/user/getUserIdByInvitationCode/{invitationCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Guid>(content);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool isValid, string message)> ValidateInvitationCodeAsync(string invitationCode)
    {
        try
        {
            var request = new { InvitationCode = invitationCode };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/validate-invitation", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ValidationResult>(responseContent);
                return (result?.isValid ?? false, result?.message ?? "خطا در اعتبارسنجی");
            }
            
            return (false, "خطا در ارتباط با سرویس کاربران");
        }
        catch
        {
            return (false, "خطا در ارتباط با سرویس کاربران");
        }
    }

    public async Task<UserDto?> GetUserByTelegramIdAsync(long telegramId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/user/{telegramId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<UserDto>(content);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateUserPhoneAsync(long telegramId, string phoneNumber)
    {
        try
        {
            var request = new { TelegramId = telegramId, PhoneNumber = phoneNumber };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/update-phone", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private record ValidationResult(bool isValid, string message);
}
