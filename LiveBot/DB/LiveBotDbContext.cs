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
    }

    public async Task<Guild> AddGuildAsync(LiveBotDbContext context, Guild guild)
    {
        Guild guildEntity= (await context.Guilds.AddAsync(guild)).Entity;
        await context.SaveChangesAsync();
        return guildEntity;
    }

    public async Task<User> AddUserAsync(LiveBotDbContext context, User user)
    {
        if (user.ParentDiscordId!=null)
        {
            await AddUserAsync(context, new User(user.ParentDiscordId.Value));
        }
        User userEntity = (await context.Users.AddAsync(user)).Entity;
        await context.SaveChangesAsync();
        return userEntity;
    }

    public async Task AddUbiInfoAsync(LiveBotDbContext context, UbiInfo ubiInfo)
    {
        if (await context.Users.FindAsync(ubiInfo.UserDiscordId)==null)
        {
            await AddUserAsync(context, new User(ubiInfo.UserDiscordId));
        }

        await context.UbiInfo.AddAsync(ubiInfo);
        await context.SaveChangesAsync();
    }

    public async Task<GuildUser> AddGuildUsersAsync(LiveBotDbContext context, GuildUser guildUser)
    {
        if (await context.Users.FindAsync(guildUser.UserDiscordId)==null)
        {
            await AddUserAsync(context,new User(guildUser.UserDiscordId));
        }

        if (await context.Guilds.FindAsync(guildUser.GuildId)==null)
        {
            await AddGuildAsync(context, new Guild(guildUser.GuildId));
        }

        GuildUser guildUserEntry = (await context.GuildUsers.AddAsync(guildUser)).Entity;
        await context.SaveChangesAsync();
        return guildUserEntry;
    }

    public async Task<UserActivity> AddUserActivityAsync(LiveBotDbContext context, UserActivity userActivity)
    {
        if (await context.GuildUsers.FindAsync(new object[]{userActivity.UserDiscordId,userActivity.GuildId})==null)
        {
            await AddGuildUsersAsync(context, new GuildUser(userActivity.UserDiscordId, userActivity.GuildId));
        }

        UserActivity entity = (await context.UserActivity.AddAsync(userActivity)).Entity;
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task AddModMailAsync(LiveBotDbContext context, ModMail modMail)
    {
        if (await context.GuildUsers.FindAsync(new object[]{modMail.UserDiscordId,modMail.GuildId})==null)
        {
            await AddGuildUsersAsync(context, new GuildUser(modMail.UserDiscordId, modMail.GuildId));
        }

        await context.ModMail.AddAsync(modMail);
        await context.SaveChangesAsync();
    }

    public async Task AddInfractionsAsync(LiveBotDbContext context, Infraction infraction)
    {
        if (await context.GuildUsers.FindAsync(new object[]{infraction.UserId,infraction.GuildId})==null)
        {
            await AddGuildUsersAsync(context, new GuildUser(infraction.UserId, infraction.GuildId));
        }

        await context.Infractions.AddAsync(infraction);
        await context.SaveChangesAsync();
    }

    public async Task AddRankRolesAsync(LiveBotDbContext context, RankRoles rankRoles)
    {
        if (await context.Guilds.FindAsync(rankRoles.GuildId)==null)
        {
            await AddGuildAsync(context,new Guild(rankRoles.GuildId));
        }

        await context.RankRoles.AddAsync(rankRoles);
        await context.SaveChangesAsync();
    }

    public async Task AddSpamIgnoreChannelsAsync(LiveBotDbContext context, SpamIgnoreChannels spamIgnoreChannels)
    {
        
        if (await context.Guilds.FindAsync(spamIgnoreChannels.GuildId)==null)
        {
            await AddGuildAsync(context,new Guild(spamIgnoreChannels.GuildId));
        }
        await context.SpamIgnoreChannels.AddAsync(spamIgnoreChannels);
        await context.SaveChangesAsync();
    }

    public async Task AddStreamNotificationsAsync(LiveBotDbContext context, StreamNotifications streamNotifications)
    {
        if (await context.Guilds.FindAsync(streamNotifications.GuildId)==null)
        {
            await AddGuildAsync(context,new Guild(streamNotifications.GuildId));
        }
        
        await context.StreamNotifications.AddAsync(streamNotifications);
        await context.SaveChangesAsync();
    }

    public async Task AddButtonRolesAsync(LiveBotDbContext context, ButtonRoles buttonRoles)
    {
        if (await context.Guilds.FindAsync(buttonRoles.GuildId)==null)
        {
            await AddGuildAsync(context,new Guild(buttonRoles.GuildId));
        }
        
        await context.ButtonRoles.AddAsync(buttonRoles);
        await context.SaveChangesAsync();
    }

    public async Task AddWhiteListSettingsAsync(LiveBotDbContext context, WhiteListSettings whiteListSettings)
    {
        
        if (await context.Guilds.FindAsync(whiteListSettings.GuildId)==null)
        {
            await AddGuildAsync(context,new Guild(whiteListSettings.GuildId));
        }
        
        await context.WhiteListSettings.AddAsync(whiteListSettings);
        await context.SaveChangesAsync();
    }

    public async Task AddRoleTagSettings(LiveBotDbContext context, RoleTagSettings roleTagSettings)
    {
        
        if (await context.Guilds.FindAsync(roleTagSettings.GuildId)==null)
        {
            await AddGuildAsync(context,new Guild(roleTagSettings.GuildId));
        }
        
        await context.RoleTagSettings.AddAsync(roleTagSettings);
        await context.SaveChangesAsync();
    }
}