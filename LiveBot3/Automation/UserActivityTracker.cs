using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using LiveBot.DB;

namespace LiveBot.Automation
{
    internal class UserActivityTracker
    {
        private static List<Cooldowns> CoolDowns { get; set; } = new List<Cooldowns>();

        public static async Task Add_Points(object Client, MessageCreateEventArgs e)
        {
            if (e.Guild == null || e.Author.IsBot ||e.Guild.Id != 282478449539678210) return;

            Cooldowns cooldowns = CoolDowns.FirstOrDefault(w => w.User == e.Author && w.Guild == e.Guild);
            if (cooldowns != null && cooldowns.Time.ToUniversalTime().AddMinutes(2) >= DateTime.UtcNow) return;

            if (DBLists.Leaderboard.FirstOrDefault(w=>w.ID_User==e.Author.Id)==null)
            {
                Services.LeaderboardService.QueueLeaderboardItem(e.Author, e.Guild);
                return;
            }
            UserActivity userActivity = DBLists.UserActivity.FirstOrDefault(w => w.Guild_ID == e.Guild.Id && w.User_ID == e.Author.Id && w.Date == DateTime.UtcNow.Date);
            if (userActivity == null)
            {
                DBLists.InsertUserActivity(new(e.Author.Id, e.Guild.Id, new Random().Next(25, 50), DateTime.UtcNow.Date));
                return;
            }
            userActivity.Points += new Random().Next(25, 50);
            DBLists.UpdateUserActivity(userActivity);

            CoolDowns.Remove(cooldowns);
            CoolDowns.Add(new Cooldowns(e.Author, e.Guild, DateTime.UtcNow));

            long userPoints = DBLists.UserActivity
                .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.Guild_ID == e.Guild.Id && w.User_ID == e.Author.Id)
                .Sum(w => w.Points);
            var rankRole = DBLists.RankRoles.AsParallel().Where(w => w.Server_ID == e.Guild.Id).ToList();
            var rankRoleUnder = DBLists.RankRoles.AsParallel().Where(w => w.Server_ID == e.Guild.Id && w.Server_Rank <= userPoints).OrderByDescending(w => w.Server_Rank).ToList();

            DiscordMember member = e.Author as DiscordMember;
            if (rankRoleUnder.Count != 0  && !member.Roles.Any(w=>w.Id == rankRoleUnder[0].Role_ID))
            {
                if (member.Roles.Any(w=>rankRole.Any(x=>x.Role_ID==w.Id)))
                {
                    await member.RevokeRoleAsync(member.Roles.FirstOrDefault(w => rankRole.Any(x => x.Role_ID == w.Id && w.Id != rankRoleUnder[0].Role_ID)));
                }
                await member.GrantRoleAsync(e.Guild.Roles.Values.FirstOrDefault(w => w.Id == rankRoleUnder[0].Role_ID));
                return;
            }
            if (rankRoleUnder.Count == 0 && member.Roles.Any(w => rankRole.Any(x => x.Role_ID == w.Id)))
            {
                await member.RevokeRoleAsync(member.Roles.FirstOrDefault(w => rankRole.Any(x => w.Id == x.Role_ID)));
                return;
            }
        }

        sealed private class Cooldowns
        {
            public DiscordUser User { get; set; }
            public DiscordGuild Guild { get; set; }
            public DateTime Time { get; set; }
            public Cooldowns(DiscordUser user, DiscordGuild guild, DateTime time)
            {
                User = user;
                Guild = guild;
                Time = time;
            }
        }
    }
}
