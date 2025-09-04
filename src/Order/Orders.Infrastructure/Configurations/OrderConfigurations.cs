using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations
{
    public class OrderConfigurations : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(o => o.Id);
            
            builder.Property(o => o.Asset)
                .IsRequired()
                .HasMaxLength(50);
            
            builder.Property(o => o.Amount)
                .IsRequired()
                .HasPrecision(18, 2);
            
            builder.Property(o => o.RemainingAmount)
                .IsRequired()
                .HasPrecision(18, 2);
            
            builder.Property(o => o.Price)
                .IsRequired()
                .HasPrecision(18, 2);
            
            builder.Property(o => o.UserId)
                .IsRequired();
            
            builder.Property(o => o.Side)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.Status)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.TradingType)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.Role)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.CreatedAt)
                .IsRequired();
            
            builder.Property(o => o.UpdatedAt);
            
            builder.Property(o => o.Notes)
                .HasMaxLength(500);
            
            builder.Property(o => o.ParentOrderId);
            
            // Indexes for better performance
            builder.HasIndex(o => o.Asset);
            builder.HasIndex(o => o.UserId);
            builder.HasIndex(o => o.Status);
            builder.HasIndex(o => o.Side);
            builder.HasIndex(o => o.TradingType);
            builder.HasIndex(o => o.Role);
            builder.HasIndex(o => o.CreatedAt);
            builder.HasIndex(o => o.ParentOrderId);
            
            // Composite indexes for common queries
            builder.HasIndex(o => new { o.Asset, o.TradingType, o.Role, o.Status });
            builder.HasIndex(o => new { o.UserId, o.Status });
        }
    }
}
