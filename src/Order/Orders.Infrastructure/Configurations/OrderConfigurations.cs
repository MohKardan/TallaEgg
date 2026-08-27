using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations
{
    public class OrderConfigurations : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(o => o.Id);
            
            builder.Property(o => o.Asset)
                .IsRequired()
                .HasMaxLength(50);
            
            // (28, 8) to match Trades.Quantity and Wallets.Balance, which have always been that
            // wide. At (18, 2) this column was narrower than the assets it stores: BTC's precision
            // is 8 decimal places, so SQL Server rounded 2.111 to 2.11 on the way in. Collateral is
            // locked from the quantity the caller supplied, and OrderCollateralReconciler recomputes
            // the residue from the quantity that was stored — so the difference between the two was
            // locked and never released. Gold never showed it: MAUA's precision is 2, exactly what
            // the column held.
            builder.Property(o => o.Amount)
                .IsRequired()
                .HasPrecision(28, 8);
            
            // RemainingAmount is a concurrency token: every UPDATE to an order carries
            // "WHERE RemainingAmount = <the value that was read>", so a second writer whose
            // read is stale — or who raced us — affects zero rows and EF throws
            // DbUpdateConcurrencyException instead of silently overwriting.
            //
            // This is what makes "one fill produces one trade" a database guarantee rather
            // than a code convention (issue #74). Before it, ExecuteAtomicMatchAsync claimed
            // in a comment to "re-fetch orders with lock", and did neither: there was no
            // lock, and because the same DbContext had just created the orders, EF identity
            // resolution handed back the tracked in-memory copy rather than reading the row.
            // Two matchers therefore both saw an unspent order and both matched it, and one
            // customer paid twice.
            //
            // A concurrency token rather than an UPDLOCK hint on purpose: it is enforced by
            // EF for any provider, so it holds under SQLite in tests exactly as under SQL
            // Server in production. Same reasoning as issue #42, where a primary key on
            // TradeId made "settled exactly once" structural.
            builder.Property(o => o.RemainingAmount)
                .IsRequired()
                .HasPrecision(28, 8)
                .IsConcurrencyToken();
            
            builder.Property(o => o.Price)
                .IsRequired()
                .HasPrecision(28, 8);
            
            builder.Property(o => o.UserId)
                .IsRequired();
            
            builder.Property(o => o.Side)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.Status)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.TradingType)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.Role)
                .IsRequired()
                .HasConversion<string>();
            
            builder.Property(o => o.CreatedAt)
                .IsRequired();
            
            builder.Property(o => o.UpdatedAt);
            
            builder.Property(o => o.Notes)
                .HasMaxLength(500);
            
            builder.Property(o => o.ParentOrderId);
            
            // Indexes for better performance
            builder.HasIndex(o => o.Asset);
            builder.HasIndex(o => o.UserId);
            builder.HasIndex(o => o.Status);
            builder.HasIndex(o => o.Side);
            builder.HasIndex(o => o.TradingType);
            builder.HasIndex(o => o.Role);
            builder.HasIndex(o => o.CreatedAt);
            builder.HasIndex(o => o.ParentOrderId);
            
            // Composite indexes for common queries
            builder.HasIndex(o => new { o.Asset, o.TradingType, o.Role, o.Status });
            builder.HasIndex(o => new { o.UserId, o.Status });
        }
    }
}
