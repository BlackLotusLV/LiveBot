﻿using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation
{
    internal class UserActivityTracker
    {
        private readonly ILeaderboardService _leaderboardService;
        private readonly LiveBotDbContext _dbContext;

        public UserActivityTracker(ILeaderboardService leaderboardService, LiveBotDbContext dbContext)
        {
            _leaderboardService = leaderboardService;
            _dbContext = dbContext;
        }
        private static List<Cooldown> CoolDowns { get; set; } = new List<Cooldown>();

        public async Task Add_Points(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Guild == null || e.Author.IsBot) return;
            
            Cooldown coolDown = CoolDowns.FirstOrDefault(w => w.User == e.Author && w.Guild == e.Guild);
            if (coolDown != null && coolDown.Time.ToUniversalTime().AddMinutes(2) >= DateTime.UtcNow) return;
            
            UserActivity userActivity =
                _dbContext.UserActivity.FirstOrDefault(activity => activity.UserDiscordId == e.Author.Id && activity.GuildId == e.Guild.Id && activity.Date == DateTime.UtcNow.Date) ??
                await _dbContext.AddUserActivityAsync(_dbContext, new UserActivity(e.Author.Id, e.Guild.Id, 0, DateTime.UtcNow.Date));
            
            await _dbContext.SaveChangesAsync();
            userActivity.Points += new Random().Next(25, 50);
            _dbContext.UserActivity.Update(userActivity);
            await _dbContext.SaveChangesAsync();

            CoolDowns.Remove(coolDown);
            CoolDowns.Add(new Cooldown(e.Author, e.Guild, DateTime.UtcNow));

            long userPoints = await _dbContext.UserActivity
                .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.GuildId == e.Guild.Id && w.UserDiscordId == e.Author.Id)
                .SumAsync(w => w.Points);
            var rankRole = _dbContext.RankRoles.AsParallel().Where(w => w.GuildId == e.Guild.Id).ToList();
            var rankRoleUnder = _dbContext.RankRoles.AsParallel().Where(w => w.GuildId == e.Guild.Id && w.ServerRank <= userPoints).OrderByDescending(w => w.ServerRank).ToList();

            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (rankRoleUnder.Count != 0  && member.Roles.Any(w=>w.Id == rankRoleUnder[0].RoleId))
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
            }
        }

        private sealed class Cooldown
        {
            public DiscordUser User { get; set; }
            public DiscordGuild Guild { get; set; }
            public DateTime Time { get; set; }
            public Cooldown(DiscordUser user, DiscordGuild guild, DateTime time)
            {
                User = user;
                Guild = guild;
                Time = time;
            }
        }
    }
}
