using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TallaEgg.TelegramBot.Infrastructure.Messaging;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// Records what the real bot would have sent instead of calling Telegram. Lets
/// <see cref="TallaEgg.TelegramBot.Infrastructure.BotHandler"/> run exactly as it does in
/// production — the same conversation code, same validation, same admin gating — without a
/// live Telegram connection. Built on the IBotMessenger seam added for issue #65.
/// </summary>
public sealed class FakeBotMessenger(ILogger<FakeBotMessenger> logger) : IBotMessenger
{
    private int _nextMessageId = 1;

    public Task<int> SendAsync(
        long chatId,
        string text,
        ReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default)
    {
        var preview = text.Length > 80 ? text[..80] + "…" : text;
        logger.LogDebug("[bot -> {ChatId}] {Text}", chatId, preview);
        return Task.FromResult(_nextMessageId++);
    }

    public Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AnswerCallbackAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
