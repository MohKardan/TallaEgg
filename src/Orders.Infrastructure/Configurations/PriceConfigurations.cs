using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations
{
    public class PriceConfigurations : IEntityTypeConfiguration<Price>
    {
        public void Configure(EntityTypeBuilder<Price> builder)
        {
            // Price configuration
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Asset).IsRequired();
            builder.Property(p => p.BuyPrice).IsRequired().HasPrecision(18, 2);
            builder.Property(p => p.SellPrice).IsRequired().HasPrecision(18, 2);
            builder.Property(p => p.UpdatedAt).IsRequired();
            builder.HasIndex(p => p.Asset).IsUnique();
        }
    }
}
