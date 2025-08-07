using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot;

public class AffiliateApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient;

    public AffiliateApiClient(string apiUrl, HttpClient httpClient)
    {
        _apiUrl = apiUrl;
        _httpClient = httpClient;
    }

    public async Task<(bool success, string message)> ValidateInvitationAsync(string invitationCode)
    {
        var request = new { InvitationCode = invitationCode };
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/validate-invitation", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<ValidateInvitationResponse>(respText);
                return (result?.IsValid ?? false, result?.Message ?? "خطا در بررسی کد دعوت");
            }
            return (false, $"خطا در بررسی کد دعوت: {respText}");
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    public async Task<(bool success, string message, Guid? invitationId)> UseInvitationAsync(string invitationCode, Guid usedByUserId)
    {
        var request = new
        {
            InvitationCode = invitationCode,
            UsedByUserId = usedByUserId
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/use-invitation", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<UseInvitationResponse>(respText);
                if (result?.Success == true)
                {
                    return (true, result.Message, result.InvitationId);
                }
                return (false, result?.Message ?? "خطا در استفاده از کد دعوت", null);
            }
            return (false, $"خطا در استفاده از کد دعوت: {respText}", null);
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return (false, $"خطا در ارتباط با سرور: {ex.Message}", null);
        }
    }

    public async Task<(bool success, InvitationDto? invitation)> CreateInvitationAsync(Guid createdByUserId, string type = "Regular", int maxUses = -1)
    {
        var request = new
        {
            CreatedByUserId = createdByUserId,
            Type = type,
            MaxUses = maxUses
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/create-invitation", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<CreateInvitationResponse>(respText);
                if (result?.Success == true)
                {
                    return (true, result.Invitation);
                }
                return (false, null);
            }
            return (false, null);
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return (false, null);
        }
    }

    public async Task<(bool success, string message)> UpdateUserPhoneAsync(long telegramId, string phoneNumber)
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
            var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/update-user-phone", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, "شماره تلفن با موفقیت به‌روزرسانی شد.");
            }
            return (false, $"خطا در به‌روزرسانی شماره تلفن: {respText}");
        }
        catch (Exception ex)
        {
            // Log exception here if needed
            return (false, $"خطا در ارتباط با سرور: {ex.Message}");
        }
    }

    private class ValidateInvitationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = "";
    }

    private class UseInvitationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public Guid? InvitationId { get; set; }
    }

    private class CreateInvitationResponse
    {
        public bool Success { get; set; }
        public InvitationDto? Invitation { get; set; }
    }
}

public class InvitationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
    public string Type { get; set; } = "";
}