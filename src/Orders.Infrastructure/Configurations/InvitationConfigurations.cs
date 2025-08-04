using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;

namespace Orders.Infrastructure.Configurations
{
    public class InvitationConfigurations : IEntityTypeConfiguration<Invitation>
    {
        public void Configure(EntityTypeBuilder<Invitation> builder)
        {
            // Invitation configuration
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Code).IsRequired();
            builder.HasIndex(i => i.Code).IsUnique();
            builder.Property(i => i.CreatedAt).IsRequired();

            // Invitation relationship
            builder
                .HasOne(i => i.CreatedBy)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }  
}
