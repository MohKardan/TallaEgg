using Microsoft.EntityFrameworkCore;
using TallaEgg.Core.Models;

namespace TallaEgg.Infrastructure.Data
{
    public class TallaEggDbContext : DbContext
    {
        public TallaEggDbContext(DbContextOptions<TallaEggDbContext> options) : base(options)
        {
        }

        public DbSet<Symbol> Symbols { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply configurations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TallaEggDbContext).Assembly);
        }
    }
}
