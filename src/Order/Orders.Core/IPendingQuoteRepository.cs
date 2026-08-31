namespace Orders.Core;

public interface IPendingQuoteRepository
{
    /// <summary>
    /// Records a proposal, closing any earlier one for the same symbol as superseded.
    ///
    /// <para>
    /// Superseding rather than queueing is deliberate (product owner, issue #158): an admin should
    /// always be deciding about the newest price the shop has seen, not working through a backlog
    /// of two-minute-old ones. Both writes commit together, so a symbol never has two live
    /// proposals and never briefly has none.
    /// </para>
    /// </summary>
    Task<PendingQuote> ProposeAsync(PendingQuote pendingQuote);

    /// <summary>
    /// Proposals still waiting for an answer and not yet past <see cref="PendingQuote.Lifetime"/>,
    /// newest first. This is what the bot polls to know what to ask about.
    /// </summary>
    Task<IReadOnlyList<PendingQuote>> GetAwaitingApprovalAsync();

    Task<PendingQuote?> GetAsync(Guid id);

    /// <summary>
    /// Publishes an approved proposal and records the approval, both in one transaction.
    ///
    /// Atomicity matters for the same reason it does in <see cref="IQuoteRepository.PublishAsync"/>:
    /// separately, a crash between them leaves either a published quote nobody approved or an
    /// approval that never took effect.
    /// </summary>
    Task<Quote> ApproveAsync(PendingQuote pendingQuote, Guid approvedByUserId);

    Task RejectAsync(PendingQuote pendingQuote, Guid rejectedByUserId);

    /// <summary>
    /// Closes proposals whose window has passed. Returns how many, so a caller can log it.
    ///
    /// Expiry is applied here rather than left implicit in the query so that the row says what
    /// happened: "nobody answered" and "an admin said no" are different outcomes and an operator
    /// looking at the history should be able to tell them apart.
    /// </summary>
    Task<int> ExpireStaleAsync();
}
