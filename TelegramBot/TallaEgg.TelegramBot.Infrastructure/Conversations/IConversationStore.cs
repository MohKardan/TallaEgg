namespace TallaEgg.TelegramBot.Infrastructure.Conversations;

/// <summary>
/// Where a customer is in the middle of placing an order (issue #65).
///
/// This was a <c>private readonly Dictionary&lt;long, OrderState&gt;</c> inside
/// <c>BotHandler</c>. Private and in-memory meant a test could neither put a customer
/// mid-conversation nor observe whether their state was cleaned up afterwards — and the
/// cleanup is the part that matters, because a stale state is what makes the next order
/// inherit the previous one's asset or side.
///
/// Keyed on the Telegram user id throughout. The old code keyed one write on the chat id
/// instead; in a private chat the two are equal, so it worked, but the inconsistency meant
/// the entry was created under one key and read under another the moment the bot was used
/// anywhere but a one-to-one chat.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// The customer's current conversation, starting a fresh one if they have none.
    /// </summary>
    OrderState GetOrStart(long telegramId, Guid userId);

    /// <summary>
    /// The customer's conversation, if one is in progress. Returns false when there is
    /// nothing in flight — the case every step must handle, because a customer can always
    /// tap a stale button from an old message.
    /// </summary>
    bool TryGet(long telegramId, out OrderState state);

    /// <summary>
    /// Ends the conversation. Must be called on every exit from an order flow — success,
    /// failure and cancellation alike — or the next order starts from the previous one's
    /// half-filled state.
    /// </summary>
    void Clear(long telegramId);

    /// <summary>
    /// Drops conversations that reached a confirmed order, for the periodic sweep.
    /// </summary>
    /// <returns>How many were removed.</returns>
    int ClearCompleted();

    /// <summary>Number of conversations in flight. Exists so tests can assert cleanup.</summary>
    int Count { get; }
}
