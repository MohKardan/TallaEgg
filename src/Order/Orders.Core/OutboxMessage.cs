namespace Orders.Core;

/// <summary>
/// Processing state of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}

/// <summary>
/// Transactional outbox record. Written in the SAME database transaction as the
/// business change that produced it (e.g. a matched Trade), so the intent to
/// perform a follow-up cross-service action can never be lost even if that action
/// (an HTTP call to the Wallet service) fails or the service is down.
///
/// A background processor later reads Pending rows and performs the action, then
/// marks them Completed. Because the receiver may be called more than once, the
/// receiver MUST be idempotent (keyed on <see cref="AggregateId"/>).
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }

    /// <summary>Logical event type, e.g. "TradeSettlement". Lets one processor route multiple message kinds.</summary>
    public string Type { get; private set; } = "";

    /// <summary>Id of the aggregate this message is about (the Trade.Id). Doubles as the idempotency key for the receiver.</summary>
    public Guid AggregateId { get; private set; }

    /// <summary>Serialized payload (JSON) the processor sends to the receiver. Self-contained so no re-fetch is needed.</summary>
    public string Payload { get; private set; } = "";

    public OutboxMessageStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>When the message was successfully processed. Null until then.</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Number of processing attempts made so far.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Earliest time the next attempt may run. Used for exponential backoff.</summary>
    public DateTime? NextAttemptAt { get; private set; }

    /// <summary>Last error message from a failed attempt, for diagnostics.</summary>
    public string? LastError { get; private set; }

    // EF Core
    private OutboxMessage() { }

    public static OutboxMessage Create(string type, Guid aggregateId, string payload)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be empty", nameof(type));
        if (aggregateId == Guid.Empty)
            throw new ArgumentException("AggregateId cannot be empty", nameof(aggregateId));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be empty", nameof(payload));

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            AggregateId = aggregateId,
            Payload = payload,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0,
            NextAttemptAt = DateTime.UtcNow
        };
    }

    /// <summary>Mark the message as successfully processed.</summary>
    public void MarkCompleted()
    {
        Status = OutboxMessageStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
        NextAttemptAt = null;
        LastError = null;
    }

    /// <summary>
    /// Record a failed attempt. Schedules the next attempt with exponential backoff,
    /// or marks the message Failed once <paramref name="maxRetries"/> is exhausted.
    /// </summary>
    public void MarkAttemptFailed(string error, int maxRetries, TimeSpan baseDelay)
    {
        RetryCount++;
        LastError = error;

        if (RetryCount >= maxRetries)
        {
            Status = OutboxMessageStatus.Failed;
            NextAttemptAt = null;
        }
        else
        {
            // Exponential backoff: baseDelay * 2^(RetryCount-1)
            var delayTicks = baseDelay.Ticks * (long)Math.Pow(2, RetryCount - 1);
            NextAttemptAt = DateTime.UtcNow.Add(TimeSpan.FromTicks(delayTicks));
        }
    }
}
