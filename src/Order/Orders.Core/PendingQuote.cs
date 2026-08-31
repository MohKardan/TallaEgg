using TallaEgg.Core.ErrorHandling;

namespace Orders.Core;

/// <summary>Where a quote that needs approval came from.</summary>
public enum QuoteSource
{
    /// <summary>The background publisher, from a reference price source.</summary>
    Auto = 0,

    /// <summary>An admin typing a price into the bot.</summary>
    Manual = 1
}

/// <summary>What happened to a quote that was held back for approval.</summary>
public enum PendingQuoteStatus
{
    /// <summary>Waiting for an admin. The only state in which it can still be published.</summary>
    Pending = 0,

    /// <summary>An admin approved it and it was published.</summary>
    Approved = 1,

    /// <summary>An admin rejected it; nothing was published.</summary>
    Rejected = 2,

    /// <summary>Nobody answered in time. See <see cref="PendingQuote.Lifetime"/>.</summary>
    Expired = 3,

    /// <summary>A newer out-of-band price arrived for the same symbol before this one was answered.</summary>
    Superseded = 4
}

/// <summary>
/// A quote that fell outside the plausibility band and is waiting for an admin to say whether it
/// is a real price (issue #158).
///
/// <para>
/// It is deliberately <b>not</b> a <see cref="Quote"/>: nothing here is tradeable, and until
/// somebody approves it no row reaches the Quotes table at all. That is the whole point — the
/// earlier design published nothing but also told nobody, so a stopped symbol simply went quiet.
/// </para>
///
/// <para>
/// It replaces the "stop after three rejections" rule that shipped first. That rule turned a
/// suspicious price into an outage, and worse, could be walked around: deactivating the quote left
/// the symbol with no anchor, so re-enabling auto-quote published the very price that had been
/// refused, through the cold-start path and with no check at all. Asking a human keeps the symbol
/// quoting from its last good price while the question is open.
/// </para>
/// </summary>
public class PendingQuote
{
    /// <summary>
    /// How long an admin has to answer before the price is too old to publish.
    ///
    /// <para>
    /// Five minutes, set by the product owner. Gold moves: a price approved half an hour after it
    /// was proposed is not the market any more, and publishing it would be its own version of the
    /// mistake this whole mechanism exists to prevent. Two auto-quote ticks fit inside the window.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public Guid Id { get; private set; }

    /// <summary>Symbol in BASE/QUOTE form, for example MAUA/IRT.</summary>
    public string Symbol { get; private set; } = string.Empty;

    /// <summary>The price the shop would buy at, if this is approved.</summary>
    public decimal BuyPrice { get; private set; }

    /// <summary>The price the shop would sell at, if this is approved.</summary>
    public decimal SellPrice { get; private set; }

    /// <summary>The midpoint of the two prices above — what the band actually measured.</summary>
    public decimal ProposedMid { get; private set; }

    /// <summary>The mid this was compared against, or null if the symbol had no quote.</summary>
    public decimal? PreviousMid { get; private set; }

    /// <summary>How far <see cref="ProposedMid"/> sits from <see cref="PreviousMid"/>, as a percentage.</summary>
    public decimal DeviationPercent { get; private set; }

    public QuoteSource Source { get; private set; }

    /// <summary>
    /// Who the quote would be published as. For a manual quote that is the admin who typed it; for
    /// an automatic one, whoever last configured auto-quote for the symbol, which is who an
    /// auto-published quote is already attributed to.
    /// </summary>
    public Guid ProposedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public PendingQuoteStatus Status { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    /// <summary>The admin who approved or rejected it; null while pending, and for expiry or supersession.</summary>
    public Guid? ResolvedByUserId { get; private set; }

    /// <summary>
    /// Optimistic concurrency, the same device <c>Order.RemainingAmount</c> and
    /// <c>WalletEntity.Version</c> use: it puts "AND Version = @read" into the UPDATE, so a second
    /// admin answering at the same moment matches zero rows and is refused rather than publishing
    /// a second quote for the symbol.
    ///
    /// <para>
    /// Checking <see cref="Status"/> in memory is not enough. Two buttons pressed together both
    /// read Pending, both pass, and both publish — the read-then-act race that #42 closed for
    /// settlement, arriving here through a different door.
    /// </para>
    /// </summary>
    public long Version { get; private set; }

    /// <summary>EF Core requires a parameterless constructor.</summary>
    private PendingQuote() { }

    public static PendingQuote Propose(
        string symbol,
        decimal buyPrice,
        decimal sellPrice,
        decimal? previousMid,
        decimal deviationPercent,
        QuoteSource source,
        Guid proposedByUserId)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new BusinessRuleException("نماد نمی‌تواند خالی باشد.");

        // The same validation Quote.Publish applies, run now rather than at approval time. A
        // proposal that could never become a quote should not be put in front of an admin at all.
        if (buyPrice <= 0)
            throw new BusinessRuleException("قیمت خرید باید بزرگ‌تر از صفر باشد.");

        if (sellPrice <= 0)
            throw new BusinessRuleException("قیمت فروش باید بزرگ‌تر از صفر باشد.");

        if (buyPrice > sellPrice)
            throw new BusinessRuleException(
                $"قیمت خرید ({buyPrice}) نمی‌تواند از قیمت فروش ({sellPrice}) بیشتر باشد.");

        if (proposedByUserId == Guid.Empty)
            throw new BusinessRuleException("شناسهٔ منتشرکننده الزامی است.");

        return new PendingQuote
        {
            Id = Guid.NewGuid(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            ProposedMid = QuotePlausibility.MidOf(buyPrice, sellPrice),
            PreviousMid = previousMid,
            DeviationPercent = deviationPercent,
            Source = source,
            ProposedByUserId = proposedByUserId,
            CreatedAt = DateTime.UtcNow,
            Status = PendingQuoteStatus.Pending
        };
    }

    /// <summary>Whether the window in <see cref="Lifetime"/> has closed on this proposal.</summary>
    public bool IsExpired(DateTime utcNow) => utcNow - CreatedAt > Lifetime;

    /// <summary>
    /// Turns an approved proposal into the quote it describes.
    ///
    /// <para>
    /// Refuses anything but a live, unexpired proposal, so a stale button in an old Telegram
    /// message cannot publish an old price — the admin who taps it has no way to know how long
    /// that message has been sitting there.
    /// </para>
    /// </summary>
    public Quote Approve(Guid approvedByUserId, DateTime utcNow)
    {
        if (Status != PendingQuoteStatus.Pending)
            throw new BusinessRuleException("این مظنه پیش‌تر بررسی شده است.");

        if (IsExpired(utcNow))
            throw new BusinessRuleException("این مظنه منقضی شده است. مظنهٔ تازه‌ای منتشر کنید.");

        if (approvedByUserId == Guid.Empty)
            throw new BusinessRuleException("شناسهٔ تأییدکننده الزامی است.");

        Status = PendingQuoteStatus.Approved;
        Version++;
        ResolvedAt = utcNow;
        ResolvedByUserId = approvedByUserId;

        // Published as the person who proposed it, not the person who approved it: approval says
        // "this price is real", and the quote's author is still whoever set the price.
        return Quote.Publish(Symbol, BuyPrice, SellPrice, ProposedByUserId);
    }

    public void Reject(Guid rejectedByUserId, DateTime utcNow)
    {
        if (Status != PendingQuoteStatus.Pending)
            throw new BusinessRuleException("این مظنه پیش‌تر بررسی شده است.");

        Status = PendingQuoteStatus.Rejected;
        Version++;
        ResolvedAt = utcNow;
        ResolvedByUserId = rejectedByUserId;
    }

    /// <summary>Closed because a newer out-of-band price arrived for the same symbol.</summary>
    public void Supersede(DateTime utcNow)
    {
        if (Status != PendingQuoteStatus.Pending) return;

        Status = PendingQuoteStatus.Superseded;
        Version++;
        ResolvedAt = utcNow;
    }

    /// <summary>Closed because nobody answered inside <see cref="Lifetime"/>.</summary>
    public void Expire(DateTime utcNow)
    {
        if (Status != PendingQuoteStatus.Pending) return;

        Status = PendingQuoteStatus.Expired;
        Version++;
        ResolvedAt = utcNow;
    }
}
