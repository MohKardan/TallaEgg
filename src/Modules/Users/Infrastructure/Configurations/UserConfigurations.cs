using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallaEgg.Api.Modules.Users.Core;

namespace TallaEgg.Api.Modules.Users.Infrastructure.Configurations;

public class UserConfigurations : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // User configuration
        builder.HasKey(u => u.Id);
        builder.Property(u => u.TelegramId).IsRequired();
        builder.HasIndex(u => u.TelegramId).IsUnique();
        builder.Property(u => u.Username).HasMaxLength(100);
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.InvitationCode).HasMaxLength(50);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.Status).HasConversion<string>();
        builder.Property(u => u.Role).HasConversion<string>();
    }
}

