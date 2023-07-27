using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation
{
    internal class MembershipScreening
    {
        private readonly IDbContextFactory _dbContextFactory;

        public MembershipScreening(IDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }
        public async Task AcceptRules(DiscordClient client, GuildMemberUpdateEventArgs e)
        {
            if (e.PendingBefore == null) return;
            if (e.PendingBefore.Value && !e.PendingAfter.Value)
            {
                await using LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
                Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id);
                if (guild?.WelcomeChannelId == null || !guild.HasScreening) return;
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