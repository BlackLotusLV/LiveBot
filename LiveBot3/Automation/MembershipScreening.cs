﻿namespace LiveBot.Automation
{
    internal static class MembershipScreening
    {
        public static async Task AcceptRules(object Client, GuildMemberUpdateEventArgs e)
        {
            if (e.PendingBefore == null) return;
            if (e.PendingBefore.Value && !e.PendingAfter.Value && e.RolesAfter.Count == 0)
            {
                var WelcomeSettings = DB.DBLists.ServerWelcomeSettings.FirstOrDefault(w => w.Server_ID == e.Guild.Id);

                if (WelcomeSettings.Channel_ID == 0 || !WelcomeSettings.HasScreening) return;
                DiscordChannel WelcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(WelcomeSettings.Channel_ID));

                if (WelcomeSettings.Welcome_Message == null) return;
                string msg = WelcomeSettings.Welcome_Message;
                msg = msg.Replace("$Mention", $"{e.Member.Mention}");
                await WelcomeChannel.SendMessageAsync(msg);

                if (WelcomeSettings.Role_ID == 0) return;
                DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(WelcomeSettings.Role_ID));
                await e.Member.GrantRoleAsync(role);
            }
        }
    }
}