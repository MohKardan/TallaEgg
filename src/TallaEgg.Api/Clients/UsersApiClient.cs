using Azure.Core;
using System.Text;
using System.Text.Json;
using Users.Core;

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

    public async Task<UserDto?> RegisterUserAsync(RegisterUserRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/register", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RegisterResponse>(responseContent);
                if (result?.success == true && result.userId.HasValue)
                {
                    // Create a basic UserDto from the registration response
                    return new UserDto(
                        result.userId.Value,
                        request.TelegramId,
                        request.Username,
                        request.FirstName,
                        request.LastName,
                        null,
                        UserStatus.Pending,
                        UserRole.RegularUser,
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        true
                    );
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserDto?> RegisterUserWithInvitationAsync(RegisterUserWithInvitationRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/register-with-invitation", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RegisterResponse>(responseContent);
                if (result?.success == true && result.userId.HasValue)
                {
                    // Create a basic UserDto from the registration response
                    return new UserDto(
                        result.userId.Value,
                        request.User.TelegramId,
                        request.User.Username,
                        request.User.FirstName,
                        request.User.LastName,
                        request.User.PhoneNumber,
                        UserStatus.Pending,
                        UserRole.RegularUser,
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        true
                    );
                }
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

    public async Task<bool> UpdateUserStatusAsync(long telegramId, UserStatus status)
    {
        try
        {
            var request = new { TelegramId = telegramId, Status = status };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/update-status", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserDto?> UpdateUserRoleAsync(Guid userId, UserRole newRole)
    {
        try
        {
            var request = new { UserId = userId, NewRole = newRole };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/update-role", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UpdateRoleResponse>(responseContent);
                if (result?.success == true)
                {
                    // Return a basic UserDto since the full user data might not be returned
                    return new UserDto(
                        userId,
                        0, // TelegramId not available in response
                        null, // Username not available in response
                        null, // FirstName not available in response
                        null, // LastName not available in response
                        null, // PhoneNumber not available in response
                        UserStatus.Active, // Default status
                        newRole,
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        true
                    );
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<UserDto>> GetUsersByRoleAsync(UserRole role)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/users/by-role/{role}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<UserDto[]>(content);
                return users ?? Array.Empty<UserDto>();
            }
            
            return Array.Empty<UserDto>();
        }
        catch
        {
            return Array.Empty<UserDto>();
        }
    }

    public async Task<bool> UserExistsAsync(long telegramId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/user/exists/{telegramId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UserExistsResponse>(content);
                return result?.exists ?? false;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    private record ValidationResult(bool isValid, string message);
    private record RegisterResponse(bool success, Guid? userId);
    private record UpdateRoleResponse(bool success, string message);
    private record UserExistsResponse(bool exists);
}
