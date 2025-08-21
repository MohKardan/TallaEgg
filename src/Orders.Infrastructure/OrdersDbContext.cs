using Microsoft.EntityFrameworkCore;
using Orders.Core;
using Orders.Infrastructure.Configurations;

namespace Orders.Infrastructure;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Trade> Trades => Set<Trade>();
   
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfigurations());
        modelBuilder.ApplyConfiguration(new TradeConfiguration());
    }
}