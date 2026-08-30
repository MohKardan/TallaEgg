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

        // Kept at decimal(18,2). #146 widened the Orders.Price column itself to decimal(28,8), so
        // this no longer needs to match the column exactly — Orders.Price can hold more precision
        // than a quote produces. It stays narrower here because nothing that creates a quote emits
        // more than 2 decimal places today.
        //
        // A quote price becomes an order price directly, so if this property carried more
        // precision than Orders.Price could hold, storing it would silently round on the way in —
        // the discrepancy behind issue #52, where the lock was computed from one price and
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
