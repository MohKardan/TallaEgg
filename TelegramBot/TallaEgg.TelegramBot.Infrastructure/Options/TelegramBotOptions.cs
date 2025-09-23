namespace TallaEgg.TelegramBot.Infrastructure.Options;

public class TelegramBotOptions
{
    public string? TelegramBotToken { get; set; }
    public string? OrderApiUrl { get; set; }
    public string? UsersApiUrl { get; set; }
    public string? AffiliateApiUrl { get; set; }
    public string? WalletApiUrl { get; set; }
    public BotSettingsOptions BotSettings { get; set; } = new();
}

public class BotSettingsOptions
{
    public bool RequireReferralCode { get; set; }
    public string DefaultReferralCode { get; set; } = "admin";
}
