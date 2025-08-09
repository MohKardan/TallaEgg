using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Requests.User;
using Telegram.Bot.Types;

namespace TallaEgg.TelegramBot;

public class UsersApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient = new();

    public UsersApiClient(string apiUrl)
    {
        _apiUrl = apiUrl;
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
        
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/user/register", content);
            var respText = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<TallaEgg.Core.DTOs.ApiResponse<UserDto>>(respText);
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
            var response = await _httpClient.PostAsync($"{_apiUrl}/user/update-phone", content);
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

    public async Task<UserDto?> GetUserAsync(long telegramId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/user/{telegramId}");
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
}

