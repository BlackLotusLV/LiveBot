using Microsoft.EntityFrameworkCore;

namespace LiveBot.DB
{
    internal class UserActivityContext : DbContext
    {
        public DbSet<UserActivity> UserActivity { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(CustomMethod.GetConnString());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserActivity>().ToTable("User_Activity");
        }
    }
}