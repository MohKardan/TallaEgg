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
            builder.Property(o => o.Asset).IsRequired();
            builder.Property(o => o.Amount).IsRequired().HasPrecision(18, 2);
            builder.Property(o => o.Price).IsRequired().HasPrecision(18, 2);
            builder.Property(o => o.UserId).IsRequired();
            builder.Property(o => o.Type).IsRequired();
            builder.Property(o => o.CreatedAt).IsRequired();
        }


    }  
}
