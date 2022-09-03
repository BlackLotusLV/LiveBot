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

        public static Task Add_Points(object Client, MessageCreateEventArgs e)
        {
            if (e.Guild == null || e.Author.IsBot) return Task.CompletedTask;

            Cooldowns cooldowns = CoolDowns.FirstOrDefault(w => w.User == e.Author && w.Guild == e.Guild);
            if (cooldowns != null && cooldowns.Time.ToUniversalTime().AddMinutes(2) >= DateTime.UtcNow) return Task.CompletedTask;

            if (DBLists.Leaderboard.FirstOrDefault(w=>w.ID_User==e.Author.Id)==null)
            {
                Services.LeaderboardService.QueueLeaderboardItem(e.Author, e.Guild);
                return Task.CompletedTask;
            }
            UserActivity userActivity = DBLists.UserActivity.FirstOrDefault(w => w.Guild_ID == e.Guild.Id && w.User_ID == e.Author.Id && w.Date == DateTime.UtcNow.Date);
            if (userActivity == null)
            {
                DBLists.InsertUserActivity(new(e.Author.Id, e.Guild.Id, 0, DateTime.UtcNow.Date));
                return Task.CompletedTask;
            }
            userActivity.Points += new Random().Next(25, 50);
            DBLists.UpdateUserActivity(userActivity);

            CoolDowns.Remove(cooldowns);
            CoolDowns.Add(new Cooldowns(e.Author, e.Guild, DateTime.UtcNow));

            return Task.CompletedTask;
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
