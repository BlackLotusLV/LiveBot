using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation
{
    internal class MembershipScreening
    {
        private readonly LiveBotDbContext _dbContext;

        public MembershipScreening(LiveBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task AcceptRules(DiscordClient client, GuildMemberUpdateEventArgs e)
        {
            if (e.PendingBefore == null) return;
            if (e.PendingBefore.Value && e.PendingAfter.Value)
            {
                Guild guild = await _dbContext.Guilds.FirstOrDefaultAsync(w => w.Id == e.Guild.Id);
                if (guild == null) return;

                if (guild.WelcomeChannelId == null || !guild.HasScreening) return;
                DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(guild.WelcomeChannelId));

                if (guild.WelcomeMessage == null) return;
                string msg = guild.WelcomeMessage;
                msg = msg.Replace("$Mention", $"{e.Member.Mention}");
                await welcomeChannel.SendMessageAsync(msg);

                if (guild.RoleId == null) return;
                DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(guild.RoleId));
                await e.Member.GrantRoleAsync(role);
            }
        }
    }
}