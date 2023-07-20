using LiveBot.DB;

namespace LiveBot.Services;

public interface IDatabaseMethodService
{
    Task<Guild> AddGuildAsync(Guild guild);
    Task<User> AddUserAsync(User user);
    Task AddUbiInfoAsync(UbiInfo ubiInfo);
    Task<GuildUser> AddGuildUsersAsync(GuildUser guildUser);
    Task<UserActivity> AddUserActivityAsync(UserActivity userActivity);
    Task AddModMailAsync(ModMail modMail);
    Task AddInfractionsAsync(Infraction infraction);
    Task AddRankRolesAsync(RankRoles rankRoles);
    Task AddSpamIgnoreChannelsAsync(SpamIgnoreChannels spamIgnoreChannels);
    Task AddStreamNotificationsAsync(StreamNotifications streamNotifications);
    Task AddButtonRolesAsync(ButtonRoles buttonRoles);
    Task AddWhiteListSettingsAsync(WhiteListSettings whiteListSettings);
    Task AddRoleTagSettings(RoleTagSettings roleTagSettings);

}

public class DatabaseMethodService : IDatabaseMethodService
{
    private readonly IDbContextFactory _dbContextFactory;
    public DatabaseMethodService(IDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }
    
    public async Task<Guild> AddGuildAsync(Guild guild)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        Guild guildEntity= (await context.Guilds.AddAsync(guild)).Entity;
        await context.SaveChangesAsync();
        return guildEntity;
    }

    public async Task<User> AddUserAsync(User user)
    {
        if (user.ParentDiscordId!=null)
        {
            await AddUserAsync(new User(user.ParentDiscordId.Value));
        }
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        User userEntity = (await context.Users.AddAsync(user)).Entity;
        await context.SaveChangesAsync();
        return userEntity;
    }

    public async Task AddUbiInfoAsync(UbiInfo ubiInfo)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Users.FindAsync(ubiInfo.UserDiscordId)==null)
        {
            await AddUserAsync(new User(ubiInfo.UserDiscordId));
        }

        await context.UbiInfo.AddAsync(ubiInfo);
        await context.SaveChangesAsync();
    }

    public async Task<GuildUser> AddGuildUsersAsync(GuildUser guildUser)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Users.FindAsync(guildUser.UserDiscordId)==null)
        {
            await AddUserAsync(new User(guildUser.UserDiscordId));
        }

        if (await context.Guilds.FindAsync(guildUser.GuildId)==null)
        {
            await AddGuildAsync(new Guild(guildUser.GuildId));
        }

        GuildUser guildUserEntry = (await context.GuildUsers.AddAsync(guildUser)).Entity;
        await context.SaveChangesAsync();
        return guildUserEntry;
    }

    public async Task<UserActivity> AddUserActivityAsync(UserActivity userActivity)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.GuildUsers.FindAsync(new object[]{userActivity.UserDiscordId,userActivity.GuildId})==null)
        {
            await AddGuildUsersAsync(new GuildUser(userActivity.UserDiscordId, userActivity.GuildId));
        }

        UserActivity entity = (await context.UserActivity.AddAsync(userActivity)).Entity;
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task AddModMailAsync(ModMail modMail)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.GuildUsers.FindAsync(new object[]{modMail.UserDiscordId,modMail.GuildId})==null)
        {
            await AddGuildUsersAsync(new GuildUser(modMail.UserDiscordId, modMail.GuildId));
        }

        await context.ModMail.AddAsync(modMail);
        await context.SaveChangesAsync();
    }

    public async Task AddInfractionsAsync(Infraction infraction)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.GuildUsers.FindAsync(new object[]{infraction.UserId,infraction.GuildId})==null)
        {
            await AddGuildUsersAsync(new GuildUser(infraction.UserId, infraction.GuildId));
        }

        await context.Infractions.AddAsync(infraction);
        await context.SaveChangesAsync();
    }

    public async Task AddRankRolesAsync(RankRoles rankRoles)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Guilds.FindAsync(rankRoles.GuildId)==null)
        {
            await AddGuildAsync(new Guild(rankRoles.GuildId));
        }

        await context.RankRoles.AddAsync(rankRoles);
        await context.SaveChangesAsync();
    }

    public async Task AddSpamIgnoreChannelsAsync(SpamIgnoreChannels spamIgnoreChannels)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Guilds.FindAsync(spamIgnoreChannels.GuildId)==null)
        {
            await AddGuildAsync(new Guild(spamIgnoreChannels.GuildId));
        }
        await context.SpamIgnoreChannels.AddAsync(spamIgnoreChannels);
        await context.SaveChangesAsync();
    }

    public async Task AddStreamNotificationsAsync(StreamNotifications streamNotifications)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Guilds.FindAsync(streamNotifications.GuildId)==null)
        {
            await AddGuildAsync(new Guild(streamNotifications.GuildId));
        }
        
        await context.StreamNotifications.AddAsync(streamNotifications);
        await context.SaveChangesAsync();
    }

    public async Task AddButtonRolesAsync(ButtonRoles buttonRoles)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Guilds.FindAsync(buttonRoles.GuildId)==null)
        {
            await AddGuildAsync(new Guild(buttonRoles.GuildId));
        }
        
        await context.ButtonRoles.AddAsync(buttonRoles);
        await context.SaveChangesAsync();
    }

    public async Task AddWhiteListSettingsAsync(WhiteListSettings whiteListSettings)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Guilds.FindAsync(whiteListSettings.GuildId)==null)
        {
            await AddGuildAsync(new Guild(whiteListSettings.GuildId));
        }
        
        await context.WhiteListSettings.AddAsync(whiteListSettings);
        await context.SaveChangesAsync();
    }

    public async Task AddRoleTagSettings(RoleTagSettings roleTagSettings)
    {
        LiveBotDbContext context = _dbContextFactory.CreateDbContext();
        if (await context.Guilds.FindAsync(roleTagSettings.GuildId)==null)
        {
            await AddGuildAsync(new Guild(roleTagSettings.GuildId));
        }
        
        await context.RoleTagSettings.AddAsync(roleTagSettings);
        await context.SaveChangesAsync();
    }
}