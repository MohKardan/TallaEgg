using Microsoft.EntityFrameworkCore;
using Orders.Core;
using Orders.Infrastructure.Configurations;

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
        modelBuilder.ApplyConfiguration(new OrderConfigurations());
        modelBuilder.ApplyConfiguration(new UserConfigurations());
        modelBuilder.ApplyConfiguration(new InvitationConfigurations());
        modelBuilder.ApplyConfiguration(new PriceConfigurations());

        SeedUsers(modelBuilder);
    }

    private void SeedUsers(ModelBuilder builder)
    {
        User user = new User()
        {
            Id = Guid.Parse("5564f136-b9fb-4719-b4dc-b0833fa24761"),
            FirstName = "مدیر",
            LastName = "کل",
            InvitationCode = "admin",
            IsActive = true,
            CreatedAt = DateTime.Parse("2025-08-04T08:43:43.1234567Z"),
        };
        builder.Entity<User>().HasData(user);
    }

}