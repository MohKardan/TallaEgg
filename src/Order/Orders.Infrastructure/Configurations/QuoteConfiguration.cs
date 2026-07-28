using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Symbol).IsRequired().HasMaxLength(20);

        // دقت قیمت باید دقیقاً همان decimal(18,2) ستون Orders.Price باشد.
        //
        // قیمت مظنه مستقیماً به قیمت سفارش تبدیل می‌شود. اگر مظنه دقت بیشتری داشته باشد،
        // همان اختلافی ساخته می‌شود که issue #52 بود: مبلغ قفل‌شده با یک قیمت حساب شود و
        // تسویه با قیمتِ گردشدهٔ دیگری.
        builder.Property(q => q.BuyPrice).IsRequired().HasPrecision(18, 2);
        builder.Property(q => q.SellPrice).IsRequired().HasPrecision(18, 2);

        builder.Property(q => q.PublishedByUserId).IsRequired();
        builder.Property(q => q.PublishedAt).IsRequired();
        builder.Property(q => q.IsActive).IsRequired();

        // «مظنهٔ فعالِ این نماد» پرتکرارترین پرس‌وجوی این جدول است: هر بار که مشتری قیمت
        // می‌بیند یا معامله می‌کند.
        builder.HasIndex(q => new { q.Symbol, q.IsActive });
    }
}
