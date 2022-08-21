using System.Collections.Concurrent;

namespace LiveBot.Services
{
    internal static class LeaderboardService
    {
        private static readonly ConcurrentQueue<LeaderboardItem> _leaderboard = new();

        private static readonly Thread thread = new(() =>
        {
            while (_leaderboard.TryDequeue(out LeaderboardItem item))
            {
                AddToServerLeaderboard(item.User, item.Guild);
            }
            Thread.Sleep(100);
        });

        public static void StartService()
        {
            thread.Start();
            Program.Client.Logger.LogInformation(CustomLogEvents.LiveBot, "Leaderboard service started!");
        }

        public static void QueueLeaderboardItem(DiscordUser user, DiscordGuild guild)
        {
            _leaderboard.Enqueue(new LeaderboardItem(user, guild));
        }

        public static void AddUserToLeaderboard(DiscordUser user)
        {
            if (DB.DBLists.Leaderboard.FirstOrDefault(w => w.ID_User == user.Id) != null) return;

            DB.Leaderboard newEntry = new()
            {
                ID_User = user.Id
            };
            DB.DBLists.InsertLeaderboard(newEntry);
        }

        public static void AddToServerLeaderboard(DiscordUser user, DiscordGuild guild)
        {
            DB.ServerRanks local = DB.DBLists.ServerRanks.AsParallel().FirstOrDefault(lb => lb.User_ID == user.Id && lb.Server_ID == guild.Id);
            if (local is null)
            {
                if (DB.DBLists.Leaderboard.FirstOrDefault(w => w.ID_User == user.Id) == null)
                {
                    AddUserToLeaderboard(user);
                }
                local = DB.DBLists.ServerRanks.AsParallel().FirstOrDefault(w => w.User_ID == user.Id && w.Server_ID == guild.Id);
                if (local is null)
                {
                    DB.ServerRanks newEntry = new()
                    {
                        User_ID = user.Id,
                        Server_ID = guild.Id,
                        Followers = 0
                    };
                    DB.DBLists.InsertServerRanks(newEntry);
                }
            }
        }

        internal class LeaderboardItem
        {
            public DiscordGuild Guild { get; set; }
            public DiscordUser User { get; set; }

            public LeaderboardItem(DiscordUser user, DiscordGuild guild)
            {
                this.Guild = guild;
                this.User = user;
            }
        }
    }
}