using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TallaEgg.TelegramBot.Infrastructure.Messaging;

/// <summary>
/// Everything the conversation handlers do to a Telegram chat, behind an interface this
/// project owns (issue #65).
///
/// <c>ITelegramBotClient</c> is injected and looks mockable, but <c>SendMessage</c>,
/// <c>EditMessageText</c>, <c>AnswerCallbackQuery</c> and <c>DeleteMessage</c> are all
/// <b>extension methods</b> on it. Extension methods are static and cannot be substituted,
/// so any test of a handler would attempt a real network call to Telegram. That single fact
/// is why the busiest class in the product had no automated coverage at all.
///
/// This interface is deliberately narrow: it is the set of operations the handlers actually
/// perform, not a mirror of the Telegram API. A wider surface would be more to fake and
/// would not buy any more coverage.
///
/// Non-messaging calls — <c>StartReceiving</c>, <c>GetMe</c>, <c>DeleteWebhook</c>,
/// <c>GetAdminUserIdsAsync</c> — stay on the raw client. They are lifecycle and lookup
/// concerns, not part of a conversation, and none of them appear in a flow worth testing.
/// </summary>
public interface IBotMessenger
{
    /// <returns>The id of the sent message, so a later edit or delete can address it.</returns>
    Task<int> SendAsync(
        long chatId,
        string text,
        ReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default);

    Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.None,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses the spinner Telegram shows on an inline button. Skipping it leaves the
    /// button visibly stuck for the customer even though the action has completed.
    /// </summary>
    Task AnswerCallbackAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        long chatId,
        int messageId,
        CancellationToken cancellationToken = default);
}
