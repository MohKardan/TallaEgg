using TallaEgg.TelegramBot.Infrastructure.Messaging;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.AllServices.Tests.Fakes;

/// <summary>
/// Records what the bot said instead of saying it (issue #65).
///
/// This is the whole point of <see cref="IBotMessenger"/>: the real send methods are
/// extension methods on <c>ITelegramBotClient</c> and cannot be substituted, so before the
/// interface existed a handler test would have opened a connection to Telegram.
///
/// Order is preserved, because in a conversation the sequence is part of the behaviour —
/// "your order was placed" arriving after "your trade executed" would be wrong even though
/// both messages are correct.
/// </summary>
public sealed class FakeBotMessenger : IBotMessenger
{
    public sealed record SentMessage(long ChatId, string Text, ReplyMarkup? ReplyMarkup);

    public List<SentMessage> Sent { get; } = [];
    public List<string> AnsweredCallbackIds { get; } = [];
    public List<(long ChatId, int MessageId)> Deleted { get; } = [];
    public List<(long ChatId, int MessageId, string Text)> Edited { get; } = [];

    /// <summary>Every message body, in the order the customer received them.</summary>
    public IReadOnlyList<string> Texts => Sent.Select(m => m.Text).ToList();

    public bool AnyTextContains(string fragment) => Sent.Any(m => m.Text.Contains(fragment));

    private int _nextMessageId = 1;

    public Task<int> SendAsync(
        long chatId,
        string text,
        ReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentMessage(chatId, text, replyMarkup));
        return Task.FromResult(_nextMessageId++);
    }

    public Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default)
    {
        Edited.Add((chatId, messageId, text));
        return Task.CompletedTask;
    }

    public Task AnswerCallbackAsync(
        string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
    {
        AnsweredCallbackIds.Add(callbackQueryId);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
    {
        Deleted.Add((chatId, messageId));
        return Task.CompletedTask;
    }
}
