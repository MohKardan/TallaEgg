using Orders.Core;
using TallaEgg.Core;
using TallaEgg.Core.ErrorHandling;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The rules a quote held for approval enforces on its own, without a database (issue #158).
///
/// <para>
/// These matter most for the button in a Telegram message, which can be tapped minutes later, by
/// any admin, possibly after somebody else has already answered. The entity is what makes those
/// cases safe; the endpoint only reports what it decides.
/// </para>
/// </summary>
public class PendingQuoteTests
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private static readonly Guid Proposer = Guid.NewGuid();

    private static PendingQuote AnyProposal(decimal buy = 995_000m, decimal sell = 1_005_000m) =>
        PendingQuote.Propose(Symbol, buy, sell, previousMid: 500_000m, deviationPercent: 100m,
            QuoteSource.Auto, Proposer);

    [Fact]
    public void AProposalStartsPendingAndRecordsWhatTheBandMeasured()
    {
        var proposal = AnyProposal();

        Assert.Equal(PendingQuoteStatus.Pending, proposal.Status);
        Assert.Equal(1_000_000m, proposal.ProposedMid);
        Assert.Equal(500_000m, proposal.PreviousMid);
        Assert.Equal(100m, proposal.DeviationPercent);
        Assert.Null(proposal.ResolvedAt);
        Assert.Null(proposal.ResolvedByUserId);
    }

    /// <summary>
    /// The same validation Quote.Publish applies, run when the proposal is made rather than when it
    /// is approved. A price that could never become a quote should not be put in front of an admin
    /// at all — they would approve it and get an error for their trouble.
    /// </summary>
    [Theory]
    [InlineData(0, 1000)]
    [InlineData(-1, 1000)]
    [InlineData(1000, 0)]
    [InlineData(1500, 1000)]   // buy above sell: the customer could round-trip out of the shop's pocket
    public void AProposalThatCouldNeverBecomeAQuoteIsRefusedUpFront(decimal buy, decimal sell)
    {
        Assert.Throws<BusinessRuleException>(() => AnyProposal(buy, sell));
    }

    [Fact]
    public void AProposalWithNoProposerIsRefused()
    {
        Assert.Throws<BusinessRuleException>(() =>
            PendingQuote.Propose(Symbol, 100m, 110m, null, 0m, QuoteSource.Auto, Guid.Empty));
    }

    [Fact]
    public void ApprovingProducesTheQuoteItDescribes()
    {
        var proposal = AnyProposal();
        var approver = Guid.NewGuid();

        var quote = proposal.Approve(approver, DateTime.UtcNow);

        Assert.Equal(Symbol, quote.Symbol);
        Assert.Equal(995_000m, quote.BuyPrice);
        Assert.Equal(1_005_000m, quote.SellPrice);

        // Attributed to whoever set the price, not whoever agreed it was real.
        Assert.Equal(Proposer, quote.PublishedByUserId);

        Assert.Equal(PendingQuoteStatus.Approved, proposal.Status);
        Assert.Equal(approver, proposal.ResolvedByUserId);
    }

    /// <summary>
    /// Two admins get the same message and both press. The second must be told the question is
    /// closed rather than publishing the price a second time.
    /// </summary>
    [Fact]
    public void ApprovingTwiceIsRefused()
    {
        var proposal = AnyProposal();
        proposal.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<BusinessRuleException>(() => proposal.Approve(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void ApprovingSomethingAlreadyRejectedIsRefused()
    {
        var proposal = AnyProposal();
        proposal.Reject(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<BusinessRuleException>(() => proposal.Approve(Guid.NewGuid(), DateTime.UtcNow));
    }

    /// <summary>
    /// Every state change bumps the concurrency token, which is what makes the database refuse a
    /// second answer rather than relying on the in-memory status check. Two admins get the same
    /// message and can press at the same instant: both read Pending, both pass the check, and
    /// without the token both would publish.
    /// </summary>
    [Fact]
    public void EveryStateChangeBumpsTheConcurrencyToken()
    {
        var approved = AnyProposal();
        var before = approved.Version;
        approved.Approve(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(before + 1, approved.Version);

        var rejected = AnyProposal();
        rejected.Reject(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(1, rejected.Version);

        var superseded = AnyProposal();
        superseded.Supersede(DateTime.UtcNow);
        Assert.Equal(1, superseded.Version);

        var expired = AnyProposal();
        expired.Expire(DateTime.UtcNow);
        Assert.Equal(1, expired.Version);
    }

    /// <summary>A proposal nobody has answered has not been written to since it was made.</summary>
    [Fact]
    public void AFreshProposalStartsAtVersionZero()
    {
        Assert.Equal(0, AnyProposal().Version);
    }

    /// <summary>
    /// The case the expiry window exists for: a button sitting in Telegram long enough that the
    /// price behind it is no longer the market. The admin tapping it has no way to know how old the
    /// message is, so the entity has to refuse.
    /// </summary>
    [Fact]
    public void ApprovingAfterTheWindowHasClosedIsRefused()
    {
        var proposal = AnyProposal();
        var tooLate = DateTime.UtcNow + PendingQuote.Lifetime + TimeSpan.FromSeconds(1);

        Assert.True(proposal.IsExpired(tooLate));
        Assert.Throws<BusinessRuleException>(() => proposal.Approve(Guid.NewGuid(), tooLate));
        Assert.Equal(PendingQuoteStatus.Pending, proposal.Status);
    }

    [Fact]
    public void ApprovingInsideTheWindowIsAllowed()
    {
        var proposal = AnyProposal();
        var justInTime = DateTime.UtcNow + PendingQuote.Lifetime - TimeSpan.FromSeconds(5);

        Assert.False(proposal.IsExpired(justInTime));
        Assert.NotNull(proposal.Approve(Guid.NewGuid(), justInTime));
    }

    /// <summary>Supersession and expiry are quiet: they close a live proposal and leave a closed one alone.</summary>
    [Fact]
    public void SupersedingAndExpiringOnlyAffectALiveProposal()
    {
        var superseded = AnyProposal();
        superseded.Supersede(DateTime.UtcNow);
        Assert.Equal(PendingQuoteStatus.Superseded, superseded.Status);

        var approved = AnyProposal();
        approved.Approve(Guid.NewGuid(), DateTime.UtcNow);
        approved.Supersede(DateTime.UtcNow);
        approved.Expire(DateTime.UtcNow);
        Assert.Equal(PendingQuoteStatus.Approved, approved.Status);
    }
}
