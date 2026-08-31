namespace Orders.Core;

/// <summary>
/// Processing state of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,

    /// <summary>
    /// A Failed message an operator has reviewed and decided will never settle (issue #39) —
    /// e.g. its collateral was consumed by later activity, or it predates a rule that now
    /// correctly refuses it. Terminal: never picked up by the processor and never re-driven,
    /// but kept (not deleted) so the record survives for reconciliation and audit.
    /// </summary>
    Abandoned = 3
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

    /// <summary>The operator's reason for abandoning this message. Null unless <see cref="Status"/> is <see cref="OutboxMessageStatus.Abandoned"/>.</summary>
    public string? AbandonReason { get; private set; }

    /// <summary>When the message was abandoned. Null unless <see cref="Status"/> is <see cref="OutboxMessageStatus.Abandoned"/>.</summary>
    public DateTime? AbandonedAt { get; private set; }

    /// <summary>
    /// The instance currently processing this message, or null if none holds it (issue #160).
    ///
    /// Written by the atomic claim in the processor, not by any method here: the claim has to be
    /// a single UPDATE with its conditions in the WHERE clause, or two instances both read "free"
    /// before either writes. Cleared here once the attempt finishes.
    /// </summary>
    public string? LeasedBy { get; private set; }

    /// <summary>
    /// When the claim on this message stops being honoured. Null if it is not claimed.
    ///
    /// The expiry is what makes this a lease rather than a lock. A processor that crashes
    /// mid-message leaves the claim behind; without an expiry the message would stay claimed
    /// forever, which for a settlement means a recorded trade nobody ever pays out and collateral
    /// locked with it. With one, the row simply becomes claimable again.
    /// </summary>
    public DateTime? LeaseExpiresAt { get; private set; }

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
        ClearLease();
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

        // Released even though the attempt failed: the backoff in NextAttemptAt already decides
        // when this message may run again, and holding the lease on top of it would only delay
        // the retry by however much of the lease was left.
        ClearLease();
    }

    /// <summary>
    /// Puts a permanently-failed message back in the queue after an operator has fixed
    /// the underlying cause. The retry counter is reset so the message gets a fresh set
    /// of attempts, and it becomes due immediately.
    ///
    /// This is safe to call even if the action actually succeeded before the failure was
    /// recorded: the receiver is idempotent on <see cref="AggregateId"/>, so a redundant
    /// delivery is a no-op rather than a double settlement.
    ///
    /// Only a Failed message can be re-driven — reviving a Completed one would be an
    /// attempt to settle the same trade twice, and a Pending one is already queued.
    /// </summary>
    public void ResetForRetry()
    {
        if (Status != OutboxMessageStatus.Failed)
            throw new InvalidOperationException(
                $"Only a failed message can be re-driven; this message is {Status}.");

        Status = OutboxMessageStatus.Pending;
        RetryCount = 0;
        NextAttemptAt = DateTime.UtcNow;
        ProcessedAt = null;

        // A message that failed while an instance held it can keep a stale claim. An operator
        // re-driving it means "run this now", so the claim goes with the rest of the old attempt.
        ClearLease();
    }

    /// <summary>
    /// Records that an operator has reviewed a permanently-failed message and decided it will
    /// never settle — e.g. its collateral no longer covers the trade, or it predates a rule
    /// that now correctly refuses it (issue #39). A reason is mandatory: this is an audited
    /// decision, not a silent drop, and the record is kept rather than deleted so the trade
    /// stays reconcilable.
    ///
    /// Only a Failed message can be abandoned, for the same reason only a Failed message can
    /// be re-driven: a Pending one hasn't exhausted its retries yet, and a Completed one
    /// already settled. Once Abandoned, <see cref="ResetForRetry"/> refuses it exactly like it
    /// already refuses a Completed message — abandoning is terminal.
    /// </summary>
    public void MarkAbandoned(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An abandon reason is required.", nameof(reason));
        if (Status != OutboxMessageStatus.Failed)
            throw new InvalidOperationException(
                $"Only a failed message can be abandoned; this message is {Status}.");

        Status = OutboxMessageStatus.Abandoned;
        AbandonReason = reason;
        AbandonedAt = DateTime.UtcNow;
        NextAttemptAt = null;
        ClearLease();
    }

    /// <summary>
    /// Gives up this instance's claim on the message (issue #160).
    ///
    /// Private because releasing is only ever the tail of finishing an attempt — completing it,
    /// failing it, re-driving it or abandoning it. A caller able to release a claim on its own
    /// could hand a message another instance is still working on to a second processor.
    /// </summary>
    private void ClearLease()
    {
        LeasedBy = null;
        LeaseExpiresAt = null;
    }
}
