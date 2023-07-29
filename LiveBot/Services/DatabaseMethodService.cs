using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

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
    Task AddPhotoCompEntryAsync(PhotoCompEntries entry);

}

public class DatabaseMethodService : IDatabaseMethodService
{
    private readonly IDbContextFactory<LiveBotDbContext> _dbContextFactory;
    public DatabaseMethodService(IDbContextFactory<LiveBotDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }
    
    public async Task<Guild> AddGuildAsync(Guild guild)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
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
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        User userEntity = (await context.Users.AddAsync(user)).Entity;
        await context.SaveChangesAsync();
        return userEntity;
    }

    public async Task AddUbiInfoAsync(UbiInfo ubiInfo)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Users.FindAsync(ubiInfo.UserDiscordId)==null)
        {
            await AddUserAsync(new User(ubiInfo.UserDiscordId));
        }
        await context.UbiInfo.AddAsync(ubiInfo);
        await context.SaveChangesAsync();
    }

    public async Task<GuildUser> AddGuildUsersAsync(GuildUser guildUser)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
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
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.GuildUsers.FindAsync(userActivity.UserDiscordId, userActivity.GuildId)==null)
        {
            await AddGuildUsersAsync(new GuildUser(userActivity.UserDiscordId, userActivity.GuildId));
        }

        UserActivity entity = (await context.UserActivity.AddAsync(userActivity)).Entity;
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task AddModMailAsync(ModMail modMail)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.GuildUsers.FindAsync(modMail.UserDiscordId, modMail.GuildId)==null)
        {
            await AddGuildUsersAsync(new GuildUser(modMail.UserDiscordId, modMail.GuildId));
        }

        await context.ModMail.AddAsync(modMail);
        await context.SaveChangesAsync();
    }

    public async Task AddInfractionsAsync(Infraction infraction)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.GuildUsers.FindAsync(infraction.UserId, infraction.GuildId)==null)
        {
            await AddGuildUsersAsync(new GuildUser(infraction.UserId, infraction.GuildId));
        }

        await context.Infractions.AddAsync(infraction);
        await context.SaveChangesAsync();
    }

    public async Task AddRankRolesAsync(RankRoles rankRoles)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Guilds.FindAsync(rankRoles.GuildId)==null)
        {
            await AddGuildAsync(new Guild(rankRoles.GuildId));
        }

        await context.RankRoles.AddAsync(rankRoles);
        await context.SaveChangesAsync();
    }

    public async Task AddSpamIgnoreChannelsAsync(SpamIgnoreChannels spamIgnoreChannels)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Guilds.FindAsync(spamIgnoreChannels.GuildId)==null)
        {
            await AddGuildAsync(new Guild(spamIgnoreChannels.GuildId));
        }
        await context.SpamIgnoreChannels.AddAsync(spamIgnoreChannels);
        await context.SaveChangesAsync();
    }

    public async Task AddStreamNotificationsAsync(StreamNotifications streamNotifications)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Guilds.FindAsync(streamNotifications.GuildId)==null)
        {
            await AddGuildAsync(new Guild(streamNotifications.GuildId));
        }
        
        await context.StreamNotifications.AddAsync(streamNotifications);
        await context.SaveChangesAsync();
    }

    public async Task AddButtonRolesAsync(ButtonRoles buttonRoles)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Guilds.FindAsync(buttonRoles.GuildId)==null)
        {
            await AddGuildAsync(new Guild(buttonRoles.GuildId));
        }
        
        await context.ButtonRoles.AddAsync(buttonRoles);
        await context.SaveChangesAsync();
    }

    public async Task AddWhiteListSettingsAsync(WhiteListSettings whiteListSettings)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Guilds.FindAsync(whiteListSettings.GuildId)==null)
        {
            await AddGuildAsync(new Guild(whiteListSettings.GuildId));
        }
        
        await context.WhiteListSettings.AddAsync(whiteListSettings);
        await context.SaveChangesAsync();
    }

    public async Task AddRoleTagSettings(RoleTagSettings roleTagSettings)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Guilds.FindAsync(roleTagSettings.GuildId)==null)
        {
            await AddGuildAsync(new Guild(roleTagSettings.GuildId));
        }
        
        await context.RoleTagSettings.AddAsync(roleTagSettings);
        await context.SaveChangesAsync();
    }

    public async Task AddPhotoCompEntryAsync(PhotoCompEntries entry)
    {
        await using LiveBotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.Users.FindAsync(entry.UserId)==null)
        {
            await AddUserAsync(new User(entry.UserId));
        }
        await context.PhotoCompEntries.AddAsync(entry);
        await context.SaveChangesAsync();
    }
}