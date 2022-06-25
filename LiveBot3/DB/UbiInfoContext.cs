using Microsoft.EntityFrameworkCore;

namespace LiveBot.DB
{
    internal class UbiInfoContext : DbContext
    {
        public DbSet<UbiInfo> UbiInfo { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(CustomMethod.GetConnString());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UbiInfo>().ToTable("Ubi_Info");
        }
    }
}
