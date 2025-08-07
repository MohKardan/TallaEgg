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
            
            builder.Property(o => o.Price)
                .IsRequired()
                .HasPrecision(18, 2);
            
            builder.Property(o => o.UserId)
                .IsRequired();
            
            builder.Property(o => o.Type)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.Status)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.CreatedAt)
                .IsRequired();
            
            builder.Property(o => o.UpdatedAt);
            
            builder.Property(o => o.Notes)
                .HasMaxLength(500);
            
            // Indexes for better performance
            builder.HasIndex(o => o.Asset);
            builder.HasIndex(o => o.UserId);
            builder.HasIndex(o => o.Status);
            builder.HasIndex(o => o.Type);
            builder.HasIndex(o => o.CreatedAt);
        }
    }
}
