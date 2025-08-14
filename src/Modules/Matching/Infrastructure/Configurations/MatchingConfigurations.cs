using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallaEgg.Api.Modules.Matching.Core;

namespace TallaEgg.Api.Modules.Matching.Infrastructure.Configurations;

public class MatchingOrderConfigurations : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Matching Order configuration
        builder.HasKey(o => o.Id);
        builder.Property(o => o.UserId).IsRequired();
        builder.Property(o => o.Asset).IsRequired().HasMaxLength(50);
        builder.Property(o => o.Amount).IsRequired().HasPrecision(18, 8);
        builder.Property(o => o.Price).IsRequired().HasPrecision(18, 8);
        builder.Property(o => o.Type).IsRequired().HasConversion<string>();
        builder.Property(o => o.Status).IsRequired().HasConversion<string>();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.OrderId).HasMaxLength(100);
        
        // Indexes
        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.Asset);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.Type);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.OrderId);
    }
}

public class TradeConfigurations : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        // Trade configuration
        builder.HasKey(t => t.Id);
        builder.Property(t => t.BuyOrderId).IsRequired();
        builder.Property(t => t.SellOrderId).IsRequired();
        builder.Property(t => t.BuyerUserId).IsRequired();
        builder.Property(t => t.SellerUserId).IsRequired();
        builder.Property(t => t.Asset).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Amount).IsRequired().HasPrecision(18, 8);
        builder.Property(t => t.Price).IsRequired().HasPrecision(18, 8);
        builder.Property(t => t.ExecutedAt).IsRequired();
        builder.Property(t => t.Fee).IsRequired().HasPrecision(18, 8);
        builder.Property(t => t.TradeId).HasMaxLength(100);
        
        // Indexes
        builder.HasIndex(t => t.BuyOrderId);
        builder.HasIndex(t => t.SellOrderId);
        builder.HasIndex(t => t.BuyerUserId);
        builder.HasIndex(t => t.SellerUserId);
        builder.HasIndex(t => t.Asset);
        builder.HasIndex(t => t.ExecutedAt);
        builder.HasIndex(t => t.TradeId);
    }
}

