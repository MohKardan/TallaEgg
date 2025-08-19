using Microsoft.EntityFrameworkCore;
using Affiliate.Core;

namespace Affiliate.Infrastructure;

public class AffiliateDbContext : DbContext
{
    public AffiliateDbContext(DbContextOptions<AffiliateDbContext> options) : base(options) { }

    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<InvitationUsage> InvitationUsages => Set<InvitationUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Invitation configuration
        modelBuilder.Entity<Invitation>().HasKey(i => i.Id);
        modelBuilder.Entity<Invitation>().Property(i => i.Code).IsRequired();
        modelBuilder.Entity<Invitation>().HasIndex(i => i.Code).IsUnique();
        modelBuilder.Entity<Invitation>().Property(i => i.CreatedAt).IsRequired();
        modelBuilder.Entity<Invitation>().Property(i => i.Type).IsRequired();

        // InvitationUsage configuration
        modelBuilder.Entity<InvitationUsage>().HasKey(iu => iu.Id);
        modelBuilder.Entity<InvitationUsage>().Property(iu => iu.UsedAt).IsRequired();
        
        // Relationship
        modelBuilder.Entity<InvitationUsage>()
            .HasOne<Invitation>()
            .WithMany()
            .HasForeignKey(iu => iu.InvitationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
} 