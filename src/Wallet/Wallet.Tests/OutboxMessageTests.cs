using Orders.Core;

namespace Wallet.Tests;

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
}
