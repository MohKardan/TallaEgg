using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations;

public class PendingQuoteConfiguration : IEntityTypeConfiguration<PendingQuote>
{
    public void Configure(EntityTypeBuilder<PendingQuote> builder)
    {
        builder.ToTable("PendingQuotes");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Symbol).IsRequired().HasMaxLength(20);

        // Same precision as Quotes, because these prices become a quote verbatim on approval.
        // Storing them any wider would let an approved proposal round on the way into Quotes.
        builder.Property(p => p.BuyPrice).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.SellPrice).IsRequired().HasPrecision(18, 2);

        // The measured figures are wider on purpose: a reference price arrives from a provider
        // with far more decimal places than a quote keeps, and rounding it here would make the
        // deviation shown to the admin disagree with the one in the log.
        builder.Property(p => p.ProposedMid).IsRequired().HasPrecision(28, 8);
        builder.Property(p => p.PreviousMid).HasPrecision(28, 8);
        builder.Property(p => p.DeviationPercent).IsRequired().HasPrecision(28, 8);

        builder.Property(p => p.Source).IsRequired();
        builder.Property(p => p.ProposedByUserId).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.ResolvedAt);
        builder.Property(p => p.ResolvedByUserId);

        // "What is still waiting for an answer" is the only hot query: the bot asks for it on
        // every poll, and the auto-publisher asks per symbol before proposing a replacement.
        builder.HasIndex(p => new { p.Status, p.CreatedAt });
        builder.HasIndex(p => new { p.Symbol, p.Status });
    }
}
