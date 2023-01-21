using Microsoft.EntityFrameworkCore;

namespace LiveBot.DB;

public class LiveBotDbContext : DbContext
{
    public LiveBotDbContext(DbContextOptions<LiveBotDbContext> options) : base(options)
    { }
    public DbSet<VehicleList> VehicleList { get; set; } //1
    public DbSet<DisciplineList> DisciplineList { get; set; } //2
    public DbSet<StreamNotifications> StreamNotifications { get; set; }//3
    public DbSet<Leaderboard> Leaderboard { get; set; }//4
    public DbSet<ServerRanks> ServerRanks { get; set; }//5
    public DbSet<Warnings> Warnings { get; set; }//6
    public DbSet<ServerSettings> ServerSettings { get; set; }//7
    public DbSet<RankRoles> RankRoles { get; set; } //8
    public DbSet<BotOutputList> BotOutputList { get; set; }//9
    public DbSet<ModMail> ModMail { get; set; } //10
    public DbSet<RoleTagSettings> RoleTagSettings { get; set; }//11
    public DbSet<ServerWelcomeSettings> ServerWelcomeSettings { get; set; } //12
    public DbSet<ButtonRoles> ButtonRoles { get; set; } //13
    public DbSet<UbiInfo> UbiInfo { get; set; }//14
    public DbSet<UserActivity> UserActivity { get; set; }//15
    public DbSet<Whitelist> WhiteList { get; set; } // 16
}