using Microsoft.EntityFrameworkCore;
using Orders.Core;
using Orders.Infrastructure.Configurations;

namespace Orders.Infrastructure;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>مظنه‌های منتشرشدهٔ ادمین؛ یک مظنهٔ فعال به‌ازای هر نماد (issue #48).</summary>
    public DbSet<Quote> Quotes => Set<Quote>();

    /// <summary>اسپرد و روشن/خاموش بودن مظنهٔ اتومات برای هر نماد (issue #90).</summary>
    public DbSet<AutoQuoteSettings> AutoQuoteSettings => Set<AutoQuoteSettings>();

    /// <summary>فعال/غیرفعال بودن هر نماد برای معامله — قابل تغییر با دستور ادمین در بات.</summary>
    public DbSet<SymbolSettings> SymbolSettings => Set<SymbolSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfigurations());
        modelBuilder.ApplyConfiguration(new TradeConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new QuoteConfiguration());
        modelBuilder.ApplyConfiguration(new AutoQuoteSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new SymbolSettingsConfiguration());
    }
}