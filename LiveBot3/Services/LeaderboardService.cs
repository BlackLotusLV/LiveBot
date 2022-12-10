using System.Collections.Concurrent;

namespace LiveBot.Services
{
    internal static class LeaderboardService
    {
        // Use a concurrent bag instead of a queue to avoid locking and improve performance
        private static readonly ConcurrentBag<LeaderboardItem> _leaderboard = new();

        // Use a CancellationTokenSource and CancellationToken to be able to stop the thread
        private static readonly CancellationTokenSource cts = new();
        private static readonly CancellationToken token = cts.Token;

        private static readonly Thread thread = new(() =>
        {
            // Check if cancellation has been requested
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_leaderboard.IsEmpty)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (_leaderboard.TryTake(out LeaderboardItem item))
                    {
                        AddToServerLeaderboard(item.User, item.Guild);
                    }
                }
                catch (Exception ex)
                {
                    Program.Client.Logger.LogError(CustomLogEvents.LiveBot,"Leaderboard Service experienced an error\n{exceptionMessage}", ex.Message);
                }
            }
        });

        public static void StartService()
        {
            thread.Start();
            Program.Client.Logger.LogInformation(CustomLogEvents.LiveBot, "Leaderboard service started!");
        }

        public static void StopService()
        {
            // Request cancellation of the thread
            cts.Cancel();

            // Wait for the thread to finish
            thread.Join();

            Program.Client.Logger.LogInformation(CustomLogEvents.LiveBot, "Leaderboard service stopped!");
        }

        public static void QueueLeaderboardItem(DiscordUser user, DiscordGuild guild)
        {
            _leaderboard.Add(new LeaderboardItem(user, guild));
        }

        public static void AddUserToLeaderboard(DiscordUser user, string locale)
        {
            if (DB.DBLists.Leaderboard.FirstOrDefault(w => w.ID_User == user.Id) != null) return;

            DB.Leaderboard newEntry = new()
            {
                ID_User = user.Id,
                Locale = locale
            };
            DB.DBLists.InsertLeaderboard(newEntry);
        }

        public static void AddToServerLeaderboard(DiscordUser user, DiscordGuild guild)
        {
            DB.ServerRanks local = DB.DBLists.ServerRanks.AsParallel().FirstOrDefault(lb => lb.User_ID == user.Id && lb.Server_ID == guild.Id);
            if (local != null) return;

            DB.ServerRanks newEntry = new()
            {
                User_ID = user.Id,
                Server_ID = guild.Id
            };
            DB.DBLists.InsertServerRanks(newEntry);
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