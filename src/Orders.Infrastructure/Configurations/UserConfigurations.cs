using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Infrastructure.Configurations
{
    public class UserConfigurations : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
           

            // User configuration
            builder.HasKey(u => u.Id);
            builder.Property(u => u.TelegramId).IsRequired();
            builder.HasIndex(u => u.TelegramId).IsUnique();
            builder.Property(u => u.CreatedAt).IsRequired();

            // User invitation relationship
            builder
                .HasOne(u => u.InvitedBy)
                .WithMany(u => u.InvitedUsers)
                .HasForeignKey(u => u.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    } 
}
