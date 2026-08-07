using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations;

public class AutoQuoteSettingsConfiguration : IEntityTypeConfiguration<AutoQuoteSettings>
{
    public void Configure(EntityTypeBuilder<AutoQuoteSettings> builder)
    {
        builder.ToTable("AutoQuoteSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Symbol).IsRequired().HasMaxLength(20);
        builder.Property(s => s.SpreadPercent).IsRequired().HasPrecision(9, 4);
        builder.Property(s => s.IsEnabled).IsRequired();
        builder.Property(s => s.UpdatedByUserId).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        // One row per symbol — the same "get or create" repository method that keeps this true
        // would otherwise let a race create two.
        builder.HasIndex(s => s.Symbol).IsUnique();
    }
}
