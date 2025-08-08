namespace TallaEgg.TelegramBot.Core.Models;

public class BotSettings
{
    public bool RequireReferralCode { get; set; } = true;
    public string DefaultReferralCode { get; set; } = "ADMIN2024";
}
