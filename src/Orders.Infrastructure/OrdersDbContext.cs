using Microsoft.EntityFrameworkCore;
using Orders.Core;

namespace Orders.Infrastructure;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Price> Prices => Set<Price>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Order configuration
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<Order>().Property(o => o.Asset).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.Amount).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Price).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.UserId).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.Type).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.CreatedAt).IsRequired();

        // User configuration
        modelBuilder.Entity<User>().HasKey(u => u.Id);
        modelBuilder.Entity<User>().Property(u => u.TelegramId).IsRequired();
        modelBuilder.Entity<User>().HasIndex(u => u.TelegramId).IsUnique();
        modelBuilder.Entity<User>().Property(u => u.CreatedAt).IsRequired();
        
        // User invitation relationship
        modelBuilder.Entity<User>()
            .HasOne(u => u.InvitedBy)
            .WithMany(u => u.InvitedUsers)
            .HasForeignKey(u => u.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Invitation configuration
        modelBuilder.Entity<Invitation>().HasKey(i => i.Id);
        modelBuilder.Entity<Invitation>().Property(i => i.Code).IsRequired();
        modelBuilder.Entity<Invitation>().HasIndex(i => i.Code).IsUnique();
        modelBuilder.Entity<Invitation>().Property(i => i.CreatedAt).IsRequired();
        
        // Invitation relationship
        modelBuilder.Entity<Invitation>()
            .HasOne(i => i.CreatedBy)
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Price configuration
        modelBuilder.Entity<Price>().HasKey(p => p.Id);
        modelBuilder.Entity<Price>().Property(p => p.Asset).IsRequired();
        modelBuilder.Entity<Price>().Property(p => p.BuyPrice).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Price>().Property(p => p.SellPrice).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Price>().Property(p => p.UpdatedAt).IsRequired();
        modelBuilder.Entity<Price>().HasIndex(p => p.Asset).IsUnique();
    }
}