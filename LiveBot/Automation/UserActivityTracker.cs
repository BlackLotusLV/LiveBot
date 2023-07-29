using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

internal class UserActivityTracker
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly IDbContextFactory<LiveBotDbContext> _dbContextFactory;
    private readonly IDatabaseMethodService _databaseMethodService;

    public UserActivityTracker(ILeaderboardService leaderboardService, IDbContextFactory<LiveBotDbContext> dbContextFactory, IDatabaseMethodService databaseMethodService)
    {
        _leaderboardService = leaderboardService;
        _dbContextFactory = dbContextFactory;
        _databaseMethodService = databaseMethodService;
    }

    private static List<Cooldown> CoolDowns { get; set; } = new();

    public async Task Add_Points(DiscordClient client, MessageCreateEventArgs e)
    {
        if (e.Guild is null || e.Author.IsBot) return;

        Cooldown coolDown = CoolDowns.FirstOrDefault(w => w.User == e.Author && w.Guild == e.Guild);
        if (coolDown != null && coolDown.Time.ToUniversalTime().AddMinutes(2) >= DateTime.UtcNow) return;

        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        UserActivity userActivity =
            liveBotDbContext.UserActivity.FirstOrDefault(activity => activity.UserDiscordId == e.Author.Id && activity.GuildId == e.Guild.Id && activity.Date == DateTime.UtcNow.Date) ??
            await _databaseMethodService.AddUserActivityAsync(new UserActivity(e.Author.Id, e.Guild.Id, 0, DateTime.UtcNow.Date));

        await liveBotDbContext.SaveChangesAsync();
        userActivity.Points += new Random().Next(25, 50);
        liveBotDbContext.UserActivity.Update(userActivity);
        await liveBotDbContext.SaveChangesAsync();

        CoolDowns.Remove(coolDown);
        CoolDowns.Add(new Cooldown(e.Author, e.Guild, DateTime.UtcNow));

        long userPoints = liveBotDbContext.UserActivity
            .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.GuildId == e.Guild.Id && w.UserDiscordId == e.Author.Id)
            .Sum(w => w.Points);
        var rankRole = liveBotDbContext.RankRoles.Where(w => w.GuildId == e.Guild.Id).ToList();
        var rankRoleUnder = liveBotDbContext.RankRoles.Where(w => w.GuildId == e.Guild.Id && w.ServerRank <= userPoints).OrderByDescending(w => w.ServerRank).ToList();
        var rankRolesOver = rankRole.Except(rankRoleUnder);

        DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);

        if (rankRoleUnder.Count == 0) return;
        if (member.Roles.Any(memberRole => memberRole.Id != rankRoleUnder.First().RoleId))
        {
            await member.GrantRoleAsync(e.Guild.Roles.Values.First(role => role.Id == rankRoleUnder.First().RoleId));
        }

        var matchingRoleList = member.Roles.Where(memberRole => rankRoleUnder.Skip(1).Any(under => memberRole.Id == under.RoleId) || rankRolesOver.Any(over => memberRole.Id == over.RoleId));
        foreach (DiscordRole discordRole in matchingRoleList)
        {
            await member.RevokeRoleAsync(discordRole);
        }

    }

    private sealed class Cooldown
    {
        public DiscordUser User { get; set; }
        public DiscordGuild Guild { get; set; }
        public DateTime Time { get; set; }

        public Cooldown(DiscordUser user, DiscordGuild guild, DateTime time)
        {
            User = user;
            Guild = guild;
            Time = time;
        }
    }
}
