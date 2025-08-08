using Microsoft.Extensions.Options;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Application.Services;

public class BotSettingsService : IBotSettingsService
{
    private readonly IOptionsMonitor<BotSettings> _settings;
    private BotSettings _currentSettings;

    public BotSettingsService(IOptionsMonitor<BotSettings> settings)
    {
        _settings = settings;
        _currentSettings = _settings.CurrentValue;
    }

    public bool RequireReferralCode => _currentSettings.RequireReferralCode;
    public string DefaultReferralCode => _currentSettings.DefaultReferralCode;

    public async Task SetRequireReferralCodeAsync(bool require)
    {
        _currentSettings.RequireReferralCode = require;
        // In a real implementation, you would save this to a database or configuration store
        await Task.CompletedTask;
    }

    public async Task SetDefaultReferralCodeAsync(string code)
    {
        _currentSettings.DefaultReferralCode = code;
        // In a real implementation, you would save this to a database or configuration store
        await Task.CompletedTask;
    }

    public async Task<BotSettings> GetSettingsAsync()
    {
        return await Task.FromResult(_currentSettings);
    }
}
