using Microsoft.EntityFrameworkCore;
using Orders.Core;

namespace Orders.Infrastructure;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<Order>().Property(o => o.Asset).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.Amount).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Price).IsRequired().HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.UserId).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.Type).IsRequired();
        modelBuilder.Entity<Order>().Property(o => o.CreatedAt).IsRequired();
    }
}