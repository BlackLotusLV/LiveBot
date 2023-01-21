namespace LiveBot.Automation
{
    internal static class MembershipScreening
    {
        public static async Task AcceptRules(object Client, GuildMemberUpdateEventArgs e)
        {
            if (e.PendingBefore == null) return;
            if (e.PendingBefore.Value && !e.PendingAfter.Value)
            {
                var WelcomeSettings = DB.DBLists.ServerWelcomeSettings.FirstOrDefault(w => w.GuildId == e.Guild.Id);
                if (WelcomeSettings == null) return;

                if (WelcomeSettings.ChannelId == 0 || !WelcomeSettings.HasScreening) return;
                DiscordChannel WelcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(WelcomeSettings.ChannelId));

                if (WelcomeSettings.WelcomeMessage == null) return;
                string msg = WelcomeSettings.WelcomeMessage;
                msg = msg.Replace("$Mention", $"{e.Member.Mention}");
                await WelcomeChannel.SendMessageAsync(msg);

                if (WelcomeSettings.RoleId == 0) return;
                DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(WelcomeSettings.RoleId));
                await e.Member.GrantRoleAsync(role);
            }
        }
    }
}