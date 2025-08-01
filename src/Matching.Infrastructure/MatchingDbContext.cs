using Microsoft.EntityFrameworkCore;
using Matching.Core;

namespace Matching.Infrastructure;

public class MatchingDbContext : DbContext
{
    public MatchingDbContext(DbContextOptions<MatchingDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Trade> Trades => Set<Trade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Order configuration
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<Order>().Property(o => o.Asset).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.Amount).IsRequired().HasPrecision(18, 8);
        modelBuilder.Entity<Order>().Property(o => o.Price).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Type).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.Status).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.CreatedAt).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.ExecutedAmount).HasPrecision(18, 8);
        modelBuilder.Entity<Order>().Property(o => o.ExecutedPrice).HasPrecision(18, 2);
        
        // Indexes for performance
        modelBuilder.Entity<Order>().HasIndex(o => new { o.Asset, o.Type, o.Status });
        modelBuilder.Entity<Order>().HasIndex(o => o.UserId);
        modelBuilder.Entity<Order>().HasIndex(o => o.OrderId).IsUnique();

        // Trade configuration
        modelBuilder.Entity<Trade>().HasKey(t => t.Id);
        modelBuilder.Entity<Trade>().Property(t => t.Asset).IsRequired();
        modelBuilder.Entity<Trade>().Property(t => t.Amount).IsRequired().HasPrecision(18, 8);
        modelBuilder.Entity<Trade>().Property(t => t.Price).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Trade>().Property(t => t.ExecutedAt).IsRequired();
        modelBuilder.Entity<Trade>().Property(t => t.Fee).IsRequired().HasPrecision(18, 8);
        
        // Indexes for trades
        modelBuilder.Entity<Trade>().HasIndex(t => new { t.Asset, t.ExecutedAt });
        modelBuilder.Entity<Trade>().HasIndex(t => t.BuyerUserId);
        modelBuilder.Entity<Trade>().HasIndex(t => t.SellerUserId);
        modelBuilder.Entity<Trade>().HasIndex(t => t.TradeId).IsUnique();
    }
} 