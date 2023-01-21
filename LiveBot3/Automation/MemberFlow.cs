using LiveBot.DB;

namespace LiveBot.Automation
{
    internal  class MemberFlow
    {
        private readonly LiveBotDbContext _databaseContext;

        public MemberFlow(LiveBotDbContext databaseContext)
        {
            _databaseContext = databaseContext;
        }
        public async Task Welcome_Member(DiscordClient client, GuildMemberAddEventArgs e)
        {
            ServerSettings serverSettings = _databaseContext.ServerSettings.FirstOrDefault(x => x.GuildId == e.Guild.Id);
            if (serverSettings==null || serverSettings.WelcomeSettings.ChannelId==0 || serverSettings.WelcomeSettings.HasScreening) return;
            DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(serverSettings.WelcomeSettings.ChannelId));

            if (serverSettings.WelcomeSettings.WelcomeMessage == null) return;
            string msg = serverSettings.WelcomeSettings.WelcomeMessage;
            msg = msg.Replace("$Mention", $"{e.Member.Mention}");
            await welcomeChannel.SendMessageAsync(msg);

            if (serverSettings.WelcomeSettings.RoleId==0) return;
            DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(serverSettings.WelcomeSettings.RoleId));
            await e.Member.GrantRoleAsync(role);
        }

        public async Task Say_Goodbye(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            ServerSettings serverSettings = _databaseContext.ServerSettings.FirstOrDefault(x => x.GuildId == e.Guild.Id);
            bool pendingCheck = serverSettings != null && !(serverSettings.WelcomeSettings.HasScreening && e.Member.IsPending == true);
            if (serverSettings != null && serverSettings.WelcomeSettings.ChannelId != 0 && pendingCheck)
            {
                DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(serverSettings.WelcomeSettings.ChannelId));
                if (serverSettings.WelcomeSettings.GoodbyeMessage != null)
                {
                    string msg = serverSettings.WelcomeSettings.GoodbyeMessage;
                    msg = msg.Replace("$Username", $"{e.Member.Username}");
                    await welcomeChannel.SendMessageAsync(msg);
                }
            }
            DB.ModMail modMailEntry = _databaseContext.ModMail.FirstOrDefault(w => w.UserDiscordId == e.Member.Id && w.GuildId == e.Guild.Id && w.IsActive);
            if (modMailEntry != null)
            {
                await ModMail.CloseModMailAsync(modMailEntry, (DiscordUser)e.Member, "Mod Mail entry closed due to user leaving", "**Mod Mail closed!\n----------------------------------------------------**");
            }
        }
    }
}