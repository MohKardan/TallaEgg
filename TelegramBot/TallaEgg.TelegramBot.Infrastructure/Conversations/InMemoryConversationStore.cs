using System.Collections.Concurrent;

namespace TallaEgg.TelegramBot.Infrastructure.Conversations;

/// <summary>
/// Keeps conversations in process memory.
///
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/>, not <c>Dictionary</c>. The
/// original field was a plain <c>Dictionary</c> written from two places at once: the
/// message-handling path, and an hourly background loop started in the constructor that
/// enumerated the keys and removed from them. Mutating a <c>Dictionary</c> while another
/// thread enumerates it can throw, and concurrent writes can corrupt its internal buckets
/// outright — a failure that would surface as an unrelated exception, or as a lost
/// conversation, long after the fact.
///
/// State is deliberately not persisted. A restart drops every in-flight conversation,
/// which is the right outcome: prices move, and resuming an order the customer began
/// before a restart would confirm it at a stale price.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<long, OrderState> _states = new();

    public int Count => _states.Count;

    public OrderState GetOrStart(long telegramId, Guid userId) =>
        _states.GetOrAdd(telegramId, _ => new OrderState { UserId = userId });

    public bool TryGet(long telegramId, out OrderState state) =>
        _states.TryGetValue(telegramId, out state!);

    public void Clear(long telegramId) => _states.TryRemove(telegramId, out _);

    public int ClearCompleted()
    {
        // Snapshot the keys first: removing while enumerating the live collection is the
        // pattern that made the old Dictionary unsafe.
        var completed = _states
            .Where(pair => pair.Value.IsConfirmed)
            .Select(pair => pair.Key)
            .ToList();

        var removed = 0;
        foreach (var key in completed)
        {
            if (_states.TryRemove(key, out _))
                removed++;
        }

        return removed;
    }
}
