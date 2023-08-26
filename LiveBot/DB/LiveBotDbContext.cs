using LiveBot.Json;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace LiveBot.DB;

public class LiveBotDbContext : DbContext
{
    public DbSet<StreamNotifications> StreamNotifications { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<GuildUser> GuildUsers { get; set; }
    public DbSet<Infraction> Infractions { get; set; }
    public DbSet<Guild> Guilds { get; set; }
    public DbSet<RankRoles> RankRoles { get; set; }
    public DbSet<ModMail> ModMail { get; set; }
    public DbSet<RoleTagSettings> RoleTagSettings { get; set; }
    public DbSet<SpamIgnoreChannels> SpamIgnoreChannels { get; set; }
    public DbSet<ButtonRoles> ButtonRoles { get; set; }
    public DbSet<UbiInfo> UbiInfo { get; set; }
    public DbSet<UserActivity> UserActivity { get; set; }
    public DbSet<WhiteListSettings> WhiteListSettings { get; set; }
    public DbSet<WhiteList> WhiteLists { get; set; }
    public DbSet<PhotoCompSettings> PhotoCompSettings { get; set; }
    public DbSet<PhotoCompEntries> PhotoCompEntries { get; set; }
    public DbSet<Tag> Tags { get; set; }

    public LiveBotDbContext()
    {
    }
    public LiveBotDbContext(DbContextOptions<LiveBotDbContext> options) : base(options)
    {
    }
    // uncomment this when creating migrations. Comment this out when publishing bot. It overrides runtime initiation of the database.
    /*
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        using StreamReader sr  = new(File.OpenRead("ConfigFiles/DevDatabase.json"));
        string databaseString = sr.ReadToEnd();
        var database = JsonConvert.DeserializeObject<DatabaseJson>(databaseString);
        optionsBuilder.UseNpgsql($"Host={database.Host};Username={database.Username};Password={database.Password};Database={database.Database}; Port={database.Port}");
    }
    //*/

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ButtonRoles>().HasKey(br => br.Id);
        modelBuilder.Entity<Guild>().HasKey(g => g.Id);
        modelBuilder.Entity<GuildUser>().HasKey(gu => new { gu.UserDiscordId, gu.GuildId });
        modelBuilder.Entity<Infraction>().HasKey(i => i.Id);
        modelBuilder.Entity<ModMail>().HasKey(mm => mm.Id);
        modelBuilder.Entity<RankRoles>().HasKey(rr => rr.Id);
        modelBuilder.Entity<RoleTagSettings>().HasKey(rts => rts.Id);
        modelBuilder.Entity<SpamIgnoreChannels>().HasKey(sic => sic.Id);
        modelBuilder.Entity<StreamNotifications>().HasKey(sn => sn.Id);
        modelBuilder.Entity<UbiInfo>().HasKey(ui => ui.Id);
        modelBuilder.Entity<User>().HasKey(u => u.DiscordId);
        modelBuilder.Entity<UserActivity>().HasKey(ua => ua.Id);
        modelBuilder.Entity<WhiteList>().HasKey(wl => wl.Id);
        modelBuilder.Entity<WhiteListSettings>().HasKey(wls => wls.Id);
        modelBuilder.Entity<PhotoCompSettings>().HasKey(pcs => pcs.Id);
        modelBuilder.Entity<PhotoCompEntries>().HasKey(pce => pce.Id);
        modelBuilder.Entity<MediaOnlyChannels>().HasKey(moc => moc.ChannelId);
        modelBuilder.Entity<Tag>().HasKey(t=>t.Id);
        
        
        modelBuilder.Entity<User>()
            .HasOne(u => u.Parent)
            .WithMany(p => p.ChildUsers)
            .HasForeignKey(p => p.ParentDiscordId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<User>()
            .HasMany(u => u.UbiInfo)
            .WithOne(ui => ui.User)
            .HasForeignKey(ui => ui.UserDiscordId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<User>()
            .HasMany(u => u.UserGuilds)
            .WithOne(gu => gu.User)
            .HasForeignKey(gu => gu.UserDiscordId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<User>()
            .HasMany(u => u.PhotoCompEntries)
            .WithOne(pce => pce.User)
            .HasForeignKey(pce => pce.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<User>()
            .HasMany(u => u.Tags)
            .WithOne(t => t.Owner)
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        
        
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.GuildUsers)
            .WithOne(gu => gu.Guild)
            .HasForeignKey(gu => gu.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.RankRoles)
            .WithOne(rr => rr.Guild)
            .HasForeignKey(rr => rr.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.SpamIgnoreChannels)
            .WithOne(sic => sic.Guild)
            .HasForeignKey(sic => sic.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.RoleTagSettings)
            .WithOne(rts => rts.Guild)
            .HasForeignKey(rts => rts.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.ButtonRoles)
            .WithOne(br => br.Guild)
            .HasForeignKey(br => br.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.StreamNotifications)
            .WithOne(sn => sn.Guild)
            .HasForeignKey(sn => sn.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.WhiteListSettings)
            .WithOne(wls => wls.Guild)
            .HasForeignKey(wls => wls.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.PhotoCompSettings)
            .WithOne(pcs => pcs.Guild)
            .HasForeignKey(pcs => pcs.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g=>g.MediaOnlyChannels)
            .WithOne(moc=>moc.Guild)
            .HasForeignKey(moc=>moc.GuildId);
        modelBuilder.Entity<Guild>()
            .HasMany(g => g.Tags)
            .WithOne(t => t.Guild)
            .HasForeignKey(t => t.GuildId);
        

        modelBuilder.Entity<GuildUser>()
            .HasMany(gu => gu.Infractions)
            .WithOne(i => i.GuildUser)
            .HasForeignKey(i => new { i.UserId, i.GuildId });
        modelBuilder.Entity<GuildUser>()
            .HasMany(gu => gu.ModMails)
            .WithOne(mm => mm.GuildUser)
            .HasForeignKey(mm => new { mm.UserDiscordId, mm.GuildId });
        modelBuilder.Entity<GuildUser>()
            .HasMany(gu => gu.UserActivity)
            .WithOne(ua => ua.GuildUser)
            .HasForeignKey(ua => new { ua.UserDiscordId, ua.GuildId });

        modelBuilder.Entity<WhiteListSettings>()
            .HasMany(wls => wls.WhitelistedUsers)
            .WithOne(wlu => wlu.Settings)
            .HasForeignKey(wlu => wlu.WhiteListSettingsId);

        modelBuilder.Entity<PhotoCompSettings>()
            .HasMany(pcs => pcs.Entries)
            .WithOne(e => e.Competition)
            .HasForeignKey(e => e.CompetitionId);
    }
}