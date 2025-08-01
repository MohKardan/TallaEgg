using Microsoft.EntityFrameworkCore;
using Wallet.Core;

namespace Wallet.Infrastructure;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Wallet configuration
        modelBuilder.Entity<Wallet>().HasKey(w => w.Id);
        modelBuilder.Entity<Wallet>().Property(w => w.UserId).IsRequired();
        modelBuilder.Entity<Wallet>().Property(w => w.Asset).IsRequired();
        modelBuilder.Entity<Wallet>().Property(w => w.Balance).IsRequired().HasPrecision(18, 8);
        modelBuilder.Entity<Wallet>().Property(w => w.LockedBalance).IsRequired().HasPrecision(18, 8);
        modelBuilder.Entity<Wallet>().Property(w => w.CreatedAt).IsRequired();
        modelBuilder.Entity<Wallet>().Property(w => w.UpdatedAt).IsRequired();
        
        // Unique constraint for user and asset combination
        modelBuilder.Entity<Wallet>().HasIndex(w => new { w.UserId, w.Asset }).IsUnique();

        // WalletTransaction configuration
        modelBuilder.Entity<WalletTransaction>().HasKey(wt => wt.Id);
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.UserId).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Asset).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Amount).IsRequired().HasPrecision(18, 8);
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Type).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.Status).IsRequired();
        modelBuilder.Entity<WalletTransaction>().Property(wt => wt.CreatedAt).IsRequired();
        
        // Indexes for performance
        modelBuilder.Entity<WalletTransaction>().HasIndex(wt => new { wt.UserId, wt.CreatedAt });
        modelBuilder.Entity<WalletTransaction>().HasIndex(wt => new { wt.Asset, wt.CreatedAt });
        modelBuilder.Entity<WalletTransaction>().HasIndex(wt => wt.ReferenceId);
    }
} 