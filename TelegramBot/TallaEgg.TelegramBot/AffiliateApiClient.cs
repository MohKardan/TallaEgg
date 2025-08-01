using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot;

public class AffiliateApiClient
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient = new();

    public AffiliateApiClient(string apiUrl)
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
            var response = await _httpClient.PostAsync($"{_apiUrl}/affiliate/validate-invitation", content);
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
                var result = JsonConvert.DeserializeObject<dynamic>(respText);
                if (result.success)
                {
                    return (true, "کد دعوت با موفقیت استفاده شد.", Guid.Parse(result.invitationId.ToString()));
                }
                return (false, result.message.ToString(), null);
            }
            return (false, $"خطا در استفاده از کد دعوت: {respText}", null);
        }
        catch (Exception ex)
        {
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
                var result = JsonConvert.DeserializeObject<dynamic>(respText);
                if (result.success)
                {
                    var invitation = JsonConvert.DeserializeObject<InvitationDto>(result.invitation.ToString());
                    return (true, invitation);
                }
                return (false, null);
            }
            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, null);
        }
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