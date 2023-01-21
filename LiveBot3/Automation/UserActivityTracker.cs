using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation
{
    internal class UserActivityTracker
    {
        private readonly ILeaderboardService _leaderboardService;

        public UserActivityTracker(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }
        private static List<Cooldowns> CoolDowns { get; set; } = new List<Cooldowns>();

        public async Task Add_Points(object Client, MessageCreateEventArgs e)
        {
            if (e.Guild == null || e.Author.IsBot) return;

            Cooldowns cooldowns = CoolDowns.FirstOrDefault(w => w.User == e.Author && w.Guild == e.Guild);
            if (cooldowns != null && cooldowns.Time.ToUniversalTime().AddMinutes(2) >= DateTime.UtcNow) return;

            if (DBLists.Leaderboard.FirstOrDefault(w=>w.UserDiscordId==e.Author.Id)==null)
            {
                _leaderboardService.QueueLeaderboardItem(e.Author,e.Guild);
                return;
            }
            UserActivity userActivity = DBLists.UserActivity.FirstOrDefault(w => w.GuildId == e.Guild.Id && w.UserDiscordId == e.Author.Id && w.Date == DateTime.UtcNow.Date);
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
                .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.GuildId == e.Guild.Id && w.UserDiscordId == e.Author.Id)
                .Sum(w => w.Points);
            var rankRole = DBLists.RankRoles.AsParallel().Where(w => w.GuildId == e.Guild.Id).ToList();
            var rankRoleUnder = DBLists.RankRoles.AsParallel().Where(w => w.GuildId == e.Guild.Id && w.ServerRank <= userPoints).OrderByDescending(w => w.ServerRank).ToList();

            DiscordMember member = e.Author as DiscordMember;
            if (rankRoleUnder.Count != 0  && !member.Roles.Any(w=>w.Id == rankRoleUnder[0].RoleId))
            {
                if (member.Roles.Any(w=>rankRole.Any(x=>x.RoleId==w.Id)))
                {
                    await member.RevokeRoleAsync(member.Roles.FirstOrDefault(w => rankRole.Any(x => x.RoleId == w.Id && w.Id != rankRoleUnder[0].RoleId)));
                }
                await member.GrantRoleAsync(e.Guild.Roles.Values.FirstOrDefault(w => w.Id == rankRoleUnder[0].RoleId));
                return;
            }
            if (rankRoleUnder.Count == 0 && member.Roles.Any(w => rankRole.Any(x => x.RoleId == w.Id)))
            {
                await member.RevokeRoleAsync(member.Roles.FirstOrDefault(w => rankRole.Any(x => w.Id == x.RoleId)));
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
