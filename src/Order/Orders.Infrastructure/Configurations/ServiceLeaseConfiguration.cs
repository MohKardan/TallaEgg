using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations;

public class ServiceLeaseConfiguration : IEntityTypeConfiguration<ServiceLease>
{
    public void Configure(EntityTypeBuilder<ServiceLease> builder)
    {
        builder.ToTable("ServiceLeases");

        // The role is the key, which is what makes the whole mechanism work: the database refuses
        // a second row for the same role, so two instances racing to create a lease nobody holds
        // yet cannot both succeed. The loser sees a unique-key violation and reads it as "someone
        // else got there first" rather than as an error.
        builder.HasKey(l => l.Role);
        builder.Property(l => l.Role).HasMaxLength(100);

        builder.Property(l => l.Owner).IsRequired().HasMaxLength(100);
        builder.Property(l => l.AcquiredAt).IsRequired();
        builder.Property(l => l.ExpiresAt).IsRequired();
    }
}
