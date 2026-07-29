namespace TallaEgg.Core.Services;

/// <summary>
/// Diagnostic and audit messages the bot posts to its private Telegram channels
/// (issue #65).
///
/// Extracted so a conversation test does not post to a real channel — and, more to the
/// point, so a missing logger cannot stop a test from constructing the handler at all.
/// These calls are observational: a failure here must never change what the customer sees.
/// </summary>
public interface ITelegramLogger
{
    Task Notif(string message, string chatId = "-1002988196234", string parseMode = "");
    Task Notif<T>(string message, T dto, string chatId = "-1002988196234", string parseMode = "");
    Task LogAsync<T>(string message, T dto, string chatId = "-822161060", string parseMode = "");
    Task LogAsync(string log, string chatId = "-822161060");
    Task ErrorAsync(Exception ex, string message = "");
}
