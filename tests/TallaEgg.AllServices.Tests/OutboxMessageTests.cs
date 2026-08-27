using Orders.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Tests for the OutboxMessage state machine, in particular the re-drive path added for
/// stuck settlements: when a message exhausts its retries the trade is already recorded
/// but unsettled, with collateral still locked, so an operator must be able to queue it
/// again once the underlying cause is fixed.
/// </summary>
public class OutboxMessageTests
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(10);

    private static OutboxMessage NewMessage() =>
        OutboxMessage.Create("TradeSettlement", Guid.NewGuid(), "{}");

    private static OutboxMessage FailedMessage()
    {
        var message = NewMessage();
        for (var i = 0; i < MaxRetries; i++)
            message.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
        return message;
    }

    [Fact]
    public void MarkAttemptFailed_BecomesFailed_OnlyAfterRetriesAreExhausted()
    {
        var message = NewMessage();

        for (var i = 1; i < MaxRetries; i++)
        {
            message.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.NotNull(message.NextAttemptAt); // still scheduled for another attempt
        }

        message.MarkAttemptFailed("boom", MaxRetries, BaseDelay);

        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Null(message.NextAttemptAt); // no longer picked up by the processor
    }

    [Fact]
    public void MarkAttemptFailed_BacksOffExponentially()
    {
        var message = NewMessage();

        message.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
        var afterFirst = message.NextAttemptAt!.Value;

        message.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
        var afterSecond = message.NextAttemptAt!.Value;

        // The second wait must be materially longer than the first.
        Assert.True(afterSecond > afterFirst,
            $"expected a longer delay after the second failure (first={afterFirst:O}, second={afterSecond:O})");
    }

    [Fact]
    public void ResetForRetry_RequeuesAFailedMessageWithAFreshBudget()
    {
        var message = FailedMessage();
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);

        message.ResetForRetry();

        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.RetryCount);      // a fresh set of attempts
        Assert.NotNull(message.NextAttemptAt);    // due immediately
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public void ResetForRetry_RefusesACompletedMessage()
    {
        // Reviving a completed settlement would be an attempt to settle the same trade
        // twice. Settlement is idempotent, but the operation should still be rejected.
        var message = NewMessage();
        message.MarkCompleted();

        var ex = Assert.Throws<InvalidOperationException>(() => message.ResetForRetry());
        Assert.Contains("Completed", ex.Message);
    }

    [Fact]
    public void ResetForRetry_RefusesAPendingMessage()
    {
        var message = NewMessage();

        Assert.Throws<InvalidOperationException>(() => message.ResetForRetry());
    }

    // ── abandon (issue #39 follow-up) ───────────────────────────────────────────

    [Fact]
    public void MarkAbandoned_RecordsTheReasonAndTimestamp()
    {
        var message = FailedMessage();

        message.MarkAbandoned("collateral consumed by later activity");

        Assert.Equal(OutboxMessageStatus.Abandoned, message.Status);
        Assert.Equal("collateral consumed by later activity", message.AbandonReason);
        Assert.NotNull(message.AbandonedAt);
        Assert.Null(message.NextAttemptAt); // never picked up by the processor again
    }

    [Fact]
    public void MarkAbandoned_RequiresANonEmptyReason()
    {
        var message = FailedMessage();

        Assert.Throws<ArgumentException>(() => message.MarkAbandoned(""));
        Assert.Throws<ArgumentException>(() => message.MarkAbandoned("   "));
    }

    /// <summary>
    /// Only a Failed message can be abandoned — a Pending one hasn't exhausted its retries,
    /// and a Completed one already settled and has nothing to abandon.
    /// </summary>
    [Fact]
    public void MarkAbandoned_RefusesAMessageThatIsNotFailed()
    {
        var pending = NewMessage();
        Assert.Throws<InvalidOperationException>(() => pending.MarkAbandoned("no reason"));

        var completed = NewMessage();
        completed.MarkCompleted();
        Assert.Throws<InvalidOperationException>(() => completed.MarkAbandoned("no reason"));
    }

    /// <summary>
    /// Abandoning is terminal: it must be refused exactly like re-driving a Completed
    /// message is refused, so an abandoned trade can never be silently retried.
    /// </summary>
    [Fact]
    public void ResetForRetry_RefusesAnAbandonedMessage()
    {
        var message = FailedMessage();
        message.MarkAbandoned("will never settle");

        var ex = Assert.Throws<InvalidOperationException>(() => message.ResetForRetry());
        Assert.Contains("Abandoned", ex.Message);
    }
}
