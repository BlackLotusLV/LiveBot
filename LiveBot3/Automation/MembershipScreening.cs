using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation
{
    internal class MembershipScreening
    {
        private readonly LiveBotDbContext _dbContext;

        MembershipScreening(LiveBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task AcceptRules(DiscordClient client, GuildMemberUpdateEventArgs e)
        {
            if (e.PendingBefore == null) return;
            if (e.PendingBefore.Value && e.PendingAfter.Value)
            {
                ServerWelcomeSettings welcomeSettings = await _dbContext.ServerWelcomeSettings.FirstOrDefaultAsync(w => w.GuildId == e.Guild.Id);
                if (welcomeSettings == null) return;

                if (welcomeSettings.ChannelId == 0 || !welcomeSettings.HasScreening) return;
                DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(welcomeSettings.ChannelId));

                if (welcomeSettings.WelcomeMessage == null) return;
                string msg = welcomeSettings.WelcomeMessage;
                msg = msg.Replace("$Mention", $"{e.Member.Mention}");
                await welcomeChannel.SendMessageAsync(msg);

                if (welcomeSettings.RoleId == 0) return;
                DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(welcomeSettings.RoleId));
                await e.Member.GrantRoleAsync(role);
            }
        }
    }
}