using Microsoft.EntityFrameworkCore;
using TallaEgg.Core.Enums.User;
using Users.Core;

namespace Users.Infrastructure;

public class UsersDbContext : DbContext
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configuration
        modelBuilder.Entity<User>().HasKey(u => u.Id);
        modelBuilder.Entity<User>().Property(u => u.TelegramId).IsRequired();
        modelBuilder.Entity<User>().HasIndex(u => u.TelegramId).IsUnique();

        // One phone number, one account — the rule #303 put in the application layer, now also in
        // storage, where a race between two concurrent update-phone calls cannot slip past it.
        // Filtered because the column is nullable and SQL Server treats NULLs as equal in a unique
        // index: without the filter the accounts that have no number yet collide with each other
        // (issue #307).
        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("[PhoneNumber] IS NOT NULL");
        modelBuilder.Entity<User>().Property(u => u.CreatedAt).IsRequired();
        modelBuilder.Entity<User>().Property(u => u.Status).IsRequired();

       // SeedUsers(modelBuilder);

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
            Role = UserRole.SuperAdmin
        };
        builder.Entity<User>().HasData(user);
    }
} 