using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot;

public class UserApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient = new();

    public UserApiClient(string apiUrl)
    {
        _apiUrl = apiUrl;
    }

    public async Task<(bool success, string message)> ValidateInvitationAsync(string invitationCode)
    {
        var request = new { InvitationCode = invitationCode };
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/user/validate-invitation", content);
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<dynamic>(respText);
                return (result.isValid, result.message.ToString());
            }
            return (false, $"خطا در بررسی کد دعوت: {respText}");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    public async Task<(bool success, string message, Guid? userId)> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode)
    {
        var request = new
        {
            TelegramId = telegramId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            InvitationCode = invitationCode
        };
        
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/user/register", content);
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<dynamic>(respText);
                if (result.success)
                {
                    return (true, "ثبت‌نام با موفقیت انجام شد.", Guid.Parse(result.userId.ToString()));
                }
                return (false, result.message.ToString(), null);
            }
            return (false, $"خطا در ثبت‌نام: {respText}", null);
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرور: {ex.Message}", null);
        }
    }

    public async Task<(bool success, string message)> UpdatePhoneAsync(long telegramId, string phoneNumber)
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
            var response = await _httpClient.PostAsync($"{_apiUrl}/user/update-phone", content);
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<dynamic>(respText);
                return (result.success, result.message.ToString());
            }
            return (false, $"خطا در ثبت شماره تلفن: {respText}");
        }
        catch (Exception ex)
        {
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    public async Task<(bool success, UserDto? user)> GetUserAsync(long telegramId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/user/{telegramId}");
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var user = JsonConvert.DeserializeObject<UserDto>(respText);
                return (true, user);
            }
            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, null);
        }
    }
}

public class UserDto
{
    public Guid Id { get; set; }
    public long TelegramId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Guid? InvitedByUserId { get; set; }
    public string? InvitationCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public bool IsActive { get; set; }
} 