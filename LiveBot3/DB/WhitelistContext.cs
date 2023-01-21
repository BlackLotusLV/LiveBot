using Microsoft.EntityFrameworkCore;

namespace LiveBot.DB;

internal class WhitelistContext : DbContext
{
    public DbSet<Whitelist> Whitelist { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql(CustomMethod.GetConnString());

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Whitelist>().ToTable("Whitelist");
    }
}