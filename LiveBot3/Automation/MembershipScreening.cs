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
                ServerSettings serverSettings = await _dbContext.ServerSettings.FirstOrDefaultAsync(w => w.GuildId == e.Guild.Id);
                if (serverSettings == null) return;

                if (serverSettings.WelcomeChannelId == null || !serverSettings.HasScreening) return;
                DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(serverSettings.WelcomeChannelId));

                if (serverSettings.WelcomeMessage == null) return;
                string msg = serverSettings.WelcomeMessage;
                msg = msg.Replace("$Mention", $"{e.Member.Mention}");
                await welcomeChannel.SendMessageAsync(msg);

                if (serverSettings.RoleId == null) return;
                DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(serverSettings.RoleId));
                await e.Member.GrantRoleAsync(role);
            }
        }
    }
}