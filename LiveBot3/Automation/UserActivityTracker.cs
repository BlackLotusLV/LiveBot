﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using LiveBot.DB;
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
        private static List<Cooldowns> CoolDowns { get; set; } = new List<Cooldowns>();

        public async Task Add_Points(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Guild == null || e.Author.IsBot) return;

            Cooldowns coolDown = CoolDowns.FirstOrDefault(w => w.User == e.Author && w.Guild == e.Guild);
            if (coolDown != null && coolDown.Time.ToUniversalTime().AddMinutes(2) >= DateTime.UtcNow) return;

            if (await _dbContext.Leaderboard.FirstOrDefaultAsync(w=>w.UserDiscordId==e.Author.Id)==null)
            {
                _leaderboardService.AddToQueue(new LeaderboardService.LeaderboardItem(e.Author,e.Guild));
                return;
            }
            UserActivity userActivity = _dbContext.UserActivity.FirstOrDefault(w => w.GuildId == e.Guild.Id && w.UserDiscordId == e.Author.Id && w.Date == DateTime.UtcNow.Date);
            if (userActivity == null)
            {
                await _dbContext.UserActivity.AddAsync(new UserActivity(_dbContext, e.Author.Id, e.Guild.Id, new Random().Next(25, 50), DateTime.UtcNow.Date));
                await _dbContext.SaveChangesAsync();
                return;
            }
            userActivity.Points += new Random().Next(25, 50);
            _dbContext.UserActivity.Update(userActivity);
            await _dbContext.SaveChangesAsync();

            CoolDowns.Remove(coolDown);
            CoolDowns.Add(new Cooldowns(e.Author, e.Guild, DateTime.UtcNow));

            long userPoints = await _dbContext.UserActivity
                .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.GuildId == e.Guild.Id && w.UserDiscordId == e.Author.Id)
                .SumAsync(w => w.Points);
            var rankRole = _dbContext.RankRoles.AsParallel().Where(w => w.GuildId == e.Guild.Id).ToList();
            var rankRoleUnder = _dbContext.RankRoles.AsParallel().Where(w => w.GuildId == e.Guild.Id && w.ServerRank <= userPoints).OrderByDescending(w => w.ServerRank).ToList();

            var member = e.Author as DiscordMember;
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

        private sealed class Cooldowns
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
