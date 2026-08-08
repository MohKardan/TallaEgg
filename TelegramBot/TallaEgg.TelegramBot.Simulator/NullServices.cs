using TallaEgg.Core.Services;
using TallaEgg.TelegramBot.Infrastructure.Services;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// The real TelegramLoggerService posts to a live Telegram channel and carries a hardcoded
/// bot token in Program.cs — simulated traffic must never touch either. See ITelegramLogger's
/// own doc comment: it was extracted from BotHandler for exactly this reason (issue #65).
/// </summary>
public sealed class NullTelegramLogger : ITelegramLogger
{
    public Task Notif(string message, string chatId = "", string parseMode = "") => Task.CompletedTask;

    public Task Notif<T>(string message, T dto, string chatId = "", string parseMode = "") => Task.CompletedTask;

    public Task LogAsync<T>(string message, T dto, string chatId = "", string parseMode = "") => Task.CompletedTask;

    public Task LogAsync(string log, string chatId = "") => Task.CompletedTask;

    public Task ErrorAsync(Exception ex, string message = "") => Task.CompletedTask;
}

/// <summary>Version announcements are irrelevant to a simulation run.</summary>
public sealed class NullVersionService : IVersionService
{
    public string GetCurrentVersion() => "simulator";

    public string? GetLastAnnouncedVersion() => null;

    public void SaveAnnouncedVersion(string version) { }
}
