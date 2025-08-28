using Azure.Core;
using System.Text;
using System.Text.Json;
using Users.Core;
using Microsoft.Extensions.Logging;

namespace TallaEgg.Api.Clients;

public class UsersApiClient : IUsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<UsersApiClient> _logger;

    public UsersApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<UsersApiClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["UsersApiUrl"] ?? "http://localhost:5136";
        _logger = logger;
        
        // Configure HttpClient for better reliability
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode)
    {
        try
        {
            _logger.LogInformation("Calling Users API: GET /api/user/getUserIdByInvitationCode/{InvitationCode}", invitationCode);
            
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/user/getUserIdByInvitationCode/{invitationCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Users API response: {Content}", content);
                return JsonSerializer.Deserialize<Guid>(content);
            }
            
            _logger.LogWarning("Users API returned non-success status: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when getting user ID by invitation code: {Message}", ex.Message);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout when getting user ID by invitation code: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when getting user ID by invitation code: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<UserDto?> RegisterUserAsync(RegisterUserRequest request)
    {
        try
        {
            _logger.LogInformation("Calling Users API: POST /api/user/register for TelegramId {TelegramId}", request.TelegramId);
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/register", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Users API registration response: {Content}", responseContent);
                
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(responseContent);
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("Users API registration failed: {Message}", apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Users API registration failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            }
            
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when registering user: {Message}", ex.Message);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout when registering user: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when registering user: {Message}", ex.Message);
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
                _logger.LogInformation("Users API registration with invitation response: {Content}", responseContent);
                
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
            _logger.LogInformation("Calling Users API: GET /api/user/{TelegramId}", telegramId);
            
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/user/{telegramId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Users API response: {Content}", content);
                
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content);
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("Users API get user failed: {Message}", apiResponse?.Message);
                }
            }
            else
            {
                _logger.LogWarning("Users API returned non-success status: {StatusCode}", response.StatusCode);
            }
            
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when getting user by Telegram ID: {Message}", ex.Message);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout when getting user by Telegram ID: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when getting user by Telegram ID: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<bool> UpdateUserPhoneAsync(long telegramId, string phoneNumber)
    {
        try
        {
            _logger.LogInformation("Calling Users API: POST /api/user/update-phone for TelegramId {TelegramId}", telegramId);
            
            var request = new { TelegramId = telegramId, PhoneNumber = phoneNumber };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/user/update-phone", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Users API update phone response: {Content}", responseContent);
                
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(responseContent);
                return apiResponse?.Success == true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Users API update phone failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            }
            
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when updating user phone: {Message}", ex.Message);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout when updating user phone: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when updating user phone: {Message}", ex.Message);
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
    
    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}
