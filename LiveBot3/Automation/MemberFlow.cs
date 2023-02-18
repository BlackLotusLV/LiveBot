using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation
{
    internal  class MemberFlow
    {
        private readonly LiveBotDbContext _databaseContext;
        private readonly IModMailService _modMailService;

        public MemberFlow(LiveBotDbContext databaseContext, IModMailService modMailService)
        {
            _databaseContext = databaseContext;
            _modMailService = modMailService;

        }
        public async Task Welcome_Member(DiscordClient client, GuildMemberAddEventArgs e)
        {
            ServerSettings serverSettings = _databaseContext.ServerSettings.FirstOrDefault(x => x.GuildId == e.Guild.Id);
            if (serverSettings==null || serverSettings.WelcomeChannelId==null || serverSettings.HasScreening) return;
            DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(serverSettings.WelcomeChannelId));

            if (serverSettings.WelcomeMessage == null) return;
            string msg = serverSettings.WelcomeMessage;
            msg = msg.Replace("$Mention", $"{e.Member.Mention}");
            await welcomeChannel.SendMessageAsync(msg);

            if (serverSettings.RoleId==null) return;
            DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(serverSettings.RoleId));
            await e.Member.GrantRoleAsync(role);
        }

        public async Task Say_Goodbye(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            ServerSettings serverSettings = _databaseContext.ServerSettings.FirstOrDefault(x => x.GuildId == e.Guild.Id);
            bool pendingCheck = serverSettings != null && !(serverSettings.HasScreening && e.Member.IsPending == true);
            if (serverSettings != null && serverSettings.WelcomeChannelId != null && pendingCheck)
            {
                DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(serverSettings.WelcomeChannelId));
                if (serverSettings.GoodbyeMessage != null)
                {
                    string msg = serverSettings.GoodbyeMessage;
                    msg = msg.Replace("$Username", $"{e.Member.Username}");
                    await welcomeChannel.SendMessageAsync(msg);
                }
            }
            DB.ModMail modMailEntry = _databaseContext.ModMail.FirstOrDefault(w => w.UserDiscordId == e.Member.Id && w.GuildId == e.Guild.Id && w.IsActive);
            if (modMailEntry != null)
            {
                await _modMailService.CloseModMailAsync(client,modMailEntry, (DiscordUser)e.Member, "Mod Mail entry closed due to user leaving", "**Mod Mail closed!\n----------------------------------------------------**");
            }
        }
    }
}