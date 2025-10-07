using Microsoft.EntityFrameworkCore;
using Wallet.Core;

namespace Wallet.Infrastructure;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

    public DbSet<WalletEntity> Wallets => Set<WalletEntity>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Wallet configuration
        modelBuilder.Entity<WalletEntity>().HasKey(w => w.Id);
        modelBuilder.Entity<WalletEntity>().Property(w => w.UserId).IsRequired();
        modelBuilder.Entity<WalletEntity>().Property(w => w.Asset).IsRequired();
        modelBuilder.Entity<WalletEntity>().Property(w => w.Balance).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<WalletEntity>().Property(w => w.LockedBalance).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<WalletEntity>().Property(w => w.CreatedAt).IsRequired();
        modelBuilder.Entity<WalletEntity>().Property(w => w.UpdatedAt).IsRequired();
        
        // Unique constraint for user and asset combination
        modelBuilder.Entity<WalletEntity>().HasIndex(w => new { w.UserId, w.Asset }).IsUnique();

        // WalletTransaction configuration (legacy)
        modelBuilder.Entity<WalletTransaction>().HasKey(wt => wt.Id);
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.UserId).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Asset).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Amount).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Type).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Status).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.CreatedAt).IsRequired();
        
        // Indexes for performance
        modelBuilder.Entity<WalletTransaction>().HasIndex(wt => new { wt.UserId, wt.CreatedAt });
        modelBuilder.Entity<WalletTransaction>().HasIndex(wt => new { wt.Asset, wt.CreatedAt });
        modelBuilder.Entity<WalletTransaction>().HasIndex(wt => wt.ReferenceId);

        // Transaction configuration (new)
        modelBuilder.Entity<Transaction>().HasKey(t => t.Id);
        modelBuilder.Entity<Transaction>().Property(t => t.WalletId).IsRequired();
        modelBuilder.Entity<Transaction>().Property(t => t.Amount).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<Transaction>().Property(t => t.BallanceBefore).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<Transaction>().Property(t => t.BallanceAfter).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<Transaction>().Property(t => t.Currency).IsRequired().HasMaxLength(50);
        modelBuilder.Entity<Transaction>().Property(t => t.Type).IsRequired();
        modelBuilder.Entity<Transaction>().Property(t => t.Status).IsRequired();
        modelBuilder.Entity<Transaction>().Property(t => t.ReferenceId);
        modelBuilder.Entity<Transaction>().Property(t => t.Description).HasMaxLength(256);
        modelBuilder.Entity<Transaction>().Property(t => t.Detail); // nvarchar(max) for JSON data
        modelBuilder.Entity<Transaction>().Property(t => t.CreatedAt).IsRequired();
        modelBuilder.Entity<Transaction>().Property(t => t.UpdatedAt);

        // Foreign key relationship
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Wallet)
            .WithMany()
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        modelBuilder.Entity<Transaction>().HasIndex(t => t.WalletId);
        modelBuilder.Entity<Transaction>().HasIndex(t => t.Currency);
        modelBuilder.Entity<Transaction>().HasIndex(t => t.Type);
        modelBuilder.Entity<Transaction>().HasIndex(t => t.Status);
        modelBuilder.Entity<Transaction>().HasIndex(t => t.ReferenceId);
        modelBuilder.Entity<Transaction>().HasIndex(t => t.CreatedAt);
        modelBuilder.Entity<Transaction>().HasIndex(t => new { t.WalletId, t.CreatedAt });
        modelBuilder.Entity<Transaction>().HasIndex(t => new { t.Currency, t.CreatedAt });
        modelBuilder.Entity<Transaction>().HasIndex(t => new { t.Type, t.Status });
    }
} 
