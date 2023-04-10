using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

public class MemberKickCheck
{
    private readonly LiveBotDbContext _databaseContext;

    public MemberKickCheck(LiveBotDbContext databaseContext)
    {
        _databaseContext = databaseContext;
    }
    public async Task OnRemoved(DiscordClient client, GuildMemberRemoveEventArgs e)
    {
        DateTimeOffset time = DateTimeOffset.UtcNow;
        DateTimeOffset beforeTime = time.AddSeconds(-5);
        DateTimeOffset afterTime = time.AddSeconds(10);
        Guild guildSettings = await _databaseContext.Guilds.FirstOrDefaultAsync(x => x.Id == e.Guild.Id);
        if (guildSettings == null || guildSettings.ModerationLogChannelId == null) return;
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.Id).Value;
        var logs = await guild.GetAuditLogsAsync(5, action_type: AuditLogActionType.Kick);
        DiscordChannel wkbLog = guild.GetChannel(guildSettings.ModerationLogChannelId.Value);
        if (logs.Count == 0) return;
        if (logs[0].CreationTimestamp >= beforeTime && logs[0].CreationTimestamp <= afterTime)
        {
            await CustomMethod.SendModLogAsync(wkbLog, e.Member, $"*by {logs[0].UserResponsible.Mention}*\n**Reason:** {logs[0].Reason}", CustomMethod.ModLogType.Kick);

            GuildUser guildUser = await _databaseContext.GuildUsers.FindAsync(new object[] { e.Member.Id, e.Guild.Id }) ??
                                  await _databaseContext.AddGuildUsersAsync(_databaseContext, new GuildUser(e.Member.Id, e.Guild.Id));
            guildUser.KickCount++;
            _databaseContext.GuildUsers.Update(guildUser);
            await _databaseContext.SaveChangesAsync();

            await _databaseContext.AddInfractionsAsync(_databaseContext, new Infraction(logs[0].UserResponsible.Id, e.Member.Id, e.Guild.Id, logs[0].Reason, false, "kick"));
        }
    }
}