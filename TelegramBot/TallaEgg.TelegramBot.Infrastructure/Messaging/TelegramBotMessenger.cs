using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Messaging;

/// <summary>
/// The production <see cref="IBotMessenger"/>: a thin pass-through to
/// <c>ITelegramBotClient</c>.
///
/// Deliberately contains no logic beyond forwarding. Anything decided here would be
/// invisible to the tests that use a fake messenger, which would quietly reopen the gap
/// this abstraction exists to close.
/// </summary>
public sealed class TelegramBotMessenger : IBotMessenger
{
    private readonly ITelegramBotClient _botClient;

    public TelegramBotMessenger(ITelegramBotClient botClient) =>
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));

    public async Task<int> SendAsync(
        long chatId,
        string text,
        ReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default)
    {
        var message = await _botClient.SendMessage(
            chatId, text, parseMode, replyMarkup: replyMarkup, cancellationToken: cancellationToken);

        return message.MessageId;
    }

    public Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default) =>
        _botClient.EditMessageText(
            chatId, messageId, text, parseMode, replyMarkup: replyMarkup, cancellationToken: cancellationToken);

    public Task AnswerCallbackAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default) =>
        _botClient.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: cancellationToken);

    public Task DeleteAsync(long chatId, int messageId, CancellationToken cancellationToken = default) =>
        _botClient.DeleteMessage(chatId, messageId, cancellationToken);
}
