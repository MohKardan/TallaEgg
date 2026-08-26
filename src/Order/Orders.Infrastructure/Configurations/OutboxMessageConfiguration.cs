using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).IsRequired().HasMaxLength(100);
        builder.Property(m => m.AggregateId).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Status).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.RetryCount).IsRequired();
        builder.Property(m => m.NextAttemptAt);
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.AbandonReason).HasMaxLength(2000);
        builder.Property(m => m.AbandonedAt);

        // Hot-path query for the background processor: pending messages that are due.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });

        // Idempotency guard: at most one outbox row per aggregate per message type,
        // so a retried match cannot enqueue the same settlement twice.
        builder.HasIndex(m => new { m.Type, m.AggregateId }).IsUnique();
    }
}
