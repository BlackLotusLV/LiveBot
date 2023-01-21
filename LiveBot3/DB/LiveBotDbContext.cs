using Microsoft.EntityFrameworkCore;

namespace LiveBot.DB;

public class LiveBotDbContext : DbContext
{
    public LiveBotDbContext(DbContextOptions<LiveBotDbContext> options) : base(options)
    { }
    public DbSet<VehicleList> VehicleList { get; set; }
    public DbSet<DisciplineList> DisciplineList { get; set; }
    public DbSet<StreamNotifications> StreamNotifications { get; set; }
    public DbSet<Leaderboard> Leaderboard { get; set; }
    public DbSet<ServerRanks> ServerRanks { get; set; }
    public DbSet<Warnings> Warnings { get; set; }
    public DbSet<ServerSettings> ServerSettings { get; set; }
    public DbSet<RankRoles> RankRoles { get; set; }
    public DbSet<BotOutputList> BotOutputList { get; set; }
    public DbSet<ModMail> ModMail { get; set; }
    public DbSet<RoleTagSettings> RoleTagSettings { get; set; }
    public DbSet<ServerWelcomeSettings> ServerWelcomeSettings { get; set; }
    public DbSet<ButtonRoles> ButtonRoles { get; set; }
    public DbSet<UbiInfo> UbiInfo { get; set; }
    public DbSet<UserActivity> UserActivity { get; set; }
    public DbSet<Whitelist> WhiteList { get; set; }
}