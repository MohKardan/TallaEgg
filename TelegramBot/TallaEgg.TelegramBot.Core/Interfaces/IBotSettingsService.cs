using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IBotSettingsService
{
    bool RequireReferralCode { get; }
    string DefaultReferralCode { get; }
    Task SetRequireReferralCodeAsync(bool require);
    Task SetDefaultReferralCodeAsync(string code);
    Task<BotSettings> GetSettingsAsync();
}
