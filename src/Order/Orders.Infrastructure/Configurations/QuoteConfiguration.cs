using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Symbol).IsRequired().HasMaxLength(20);

        // Price precision must match the Orders.Price column exactly: decimal(18,2).
        //
        // A quote price becomes an order price directly. If the quote carried more precision, it
        // would recreate the discrepancy behind issue #52: the lock computed from one price and
        // settlement from a differently-rounded one.
        builder.Property(q => q.BuyPrice).IsRequired().HasPrecision(18, 2);
        builder.Property(q => q.SellPrice).IsRequired().HasPrecision(18, 2);

        builder.Property(q => q.PublishedByUserId).IsRequired();
        builder.Property(q => q.PublishedAt).IsRequired();
        builder.Property(q => q.IsActive).IsRequired();

        // "The active quote for this symbol" is this table's most frequent query — every time a
        // customer sees a price or trades.
        builder.HasIndex(q => new { q.Symbol, q.IsActive });
    }
}
