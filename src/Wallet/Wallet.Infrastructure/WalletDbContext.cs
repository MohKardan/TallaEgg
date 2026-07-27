using Microsoft.EntityFrameworkCore;
using Wallet.Core;

namespace Wallet.Infrastructure;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

    public DbSet<WalletEntity> Wallets => Set<WalletEntity>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>یک سطر به‌ازای هر معاملهٔ تسویه‌شده. کلید اصلی آن، تسویهٔ تکراری را غیرممکن می‌کند (issue #42).</summary>
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

        // TradeSettlement configuration — سد یکتایی تسویه (issue #42)
        //
        // TradeId عمداً کلید اصلی است و نه یک ستون معمولی با ایندکس یکتا: کلید اصلی
        // نیت را مستقیم بیان می‌کند («یک سطر = یک معاملهٔ تسویه‌شده») و امکان درج
        // سطر دوم برای همان معامله را در سطح موتور دیتابیس از بین می‌برد، مستقل از
        // اینکه کدام مسیر کد فراخوانی کرده باشد.
        modelBuilder.Entity<TradeSettlement>().HasKey(s => s.TradeId);

        // ValueGeneratedNever لازم است چون TradeId از سرویس سفارش‌ها می‌آید. بدون آن،
        // EF کلید Guid را به‌صورت خودکار تولیدشده فرض می‌کند و مقدار ارسالی را نادیده
        // می‌گیرد — که یعنی هر تسویه یک شناسهٔ تازه می‌گرفت و قید یکتایی هیچ‌وقت فعال نمی‌شد.
        modelBuilder.Entity<TradeSettlement>().Property(s => s.TradeId).ValueGeneratedNever();

        modelBuilder.Entity<TradeSettlement>().Property(s => s.SettledAt).IsRequired();
        modelBuilder.Entity<TradeSettlement>().Property(s => s.Symbol).IsRequired().HasMaxLength(32);
        modelBuilder.Entity<TradeSettlement>().Property(s => s.Quantity).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<TradeSettlement>().Property(s => s.QuoteQuantity).IsRequired().HasPrecision(28, 8);
        modelBuilder.Entity<TradeSettlement>().Property(s => s.BuyerUserId).IsRequired();
        modelBuilder.Entity<TradeSettlement>().Property(s => s.SellerUserId).IsRequired();

        // برای پرس‌وجوی «تسویه‌های اخیر» در مغایرت‌گیری (#39).
        modelBuilder.Entity<TradeSettlement>().HasIndex(s => s.SettledAt);
    }
}
