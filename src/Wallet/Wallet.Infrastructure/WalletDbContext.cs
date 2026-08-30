using Microsoft.EntityFrameworkCore;
using Wallet.Core;

namespace Wallet.Infrastructure;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

    public DbSet<WalletEntity> Wallets => Set<WalletEntity>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>One row per settled trade. Its primary key is what makes a duplicate settlement impossible (issue #42).</summary>
    public DbSet<TradeSettlement> TradeSettlements => Set<TradeSettlement>();

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

        // Optimistic concurrency (audit finding C-4). This is what puts "AND Version = @read"
        // into every UPDATE, which is the whole mechanism: a stale writer matches zero rows and
        // EF raises DbUpdateConcurrencyException rather than overwriting.
        modelBuilder.Entity<WalletEntity>().Property(w => w.Version).IsRequired().IsConcurrencyToken();
        
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

        // The deduplication barrier for admin top-ups and deductions (issue #157). Same role as
        // the TradeSettlements primary key below: the database, not the order in which code
        // happens to run, is what makes a second application of one reference impossible.
        //
        // WalletId is part of the key and not optional. Settling a trade writes four transaction
        // rows under one reference — the trade id — one for each of buyer/quote, buyer/base,
        // seller/base and seller/quote (WalletRepository.SettleTradeAsync). Those are four
        // different wallets, since settlement refuses a self-trade, so they stay distinct under
        // this key; a unique index on ReferenceId alone would reject every trade in the system.
        //
        // The filter is not optional either. SQL Server treats NULLs as equal in a unique index,
        // so without it a wallet could hold only one transaction with no reference — breaking
        // every lock, unlock and unreferenced deposit after the first.
        modelBuilder.Entity<Transaction>()
            .HasIndex(t => new { t.WalletId, t.ReferenceId })
            .IsUnique()
            .HasFilter("[ReferenceId] IS NOT NULL");

        // TradeSettlement configuration — the settlement uniqueness barrier (issue #42).
        //
        // TradeId is deliberately the primary key rather than an ordinary column with a unique
        // index: a primary key states the intent directly ("one row = one settled trade") and
        // makes a second row for the same trade impossible at the database engine, whichever code
        // path does the insert.
        modelBuilder.Entity<TradeSettlement>().HasKey(s => s.TradeId);

        // ValueGeneratedNever is required because TradeId comes from the Orders service. Without
        // it EF treats a Guid key as store-generated and ignores the supplied value, so every
        // settlement would get a fresh id and the uniqueness constraint would never fire.
        modelBuilder.Entity<TradeSettlement>().Property(s => s.TradeId).ValueGeneratedNever();

        modelBuilder.Entity<TradeSettlement>().Property(s => s.SettledAt).IsRequired();
        modelBuilder.Entity<TradeSettlement>().Property(s => s.Symbol).IsRequired().HasMaxLength(32);
        modelBuilder.Entity<TradeSettlement>().Property(s => s.Quantity).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<TradeSettlement>().Property(s => s.QuoteQuantity).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<TradeSettlement>().Property(s => s.BuyerUserId).IsRequired();
        modelBuilder.Entity<TradeSettlement>().Property(s => s.SellerUserId).IsRequired();

        // Supports the "recent settlements" query used by reconciliation (#39).
        modelBuilder.Entity<TradeSettlement>().HasIndex(s => s.SettledAt);
    }
}
