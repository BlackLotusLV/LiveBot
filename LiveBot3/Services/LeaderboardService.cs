using System.Collections.Concurrent;

namespace LiveBot.Services
{
    public interface ILeaderboardService
    {
        void StartService(DiscordClient client);
        void StopService(DiscordClient client);
        void QueueLeaderboardItem(DiscordUser user, DiscordGuild guild);
    }
    public class LeaderboardService : ILeaderboardService
    {
        // Use a concurrent bag instead of a queue to avoid locking and improve performance
        private static readonly ConcurrentBag<LeaderboardItem> Leaderboard = new();

        // Use a CancellationTokenSource and CancellationToken to be able to stop the thread
        private static readonly CancellationTokenSource Cts = new();
        private static readonly CancellationToken Token = Cts.Token;

        private static readonly Thread Thread = new(() =>
        {
            // Check if cancellation has been requested
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    if (Leaderboard.IsEmpty)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (Leaderboard.TryTake(out LeaderboardItem item))
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

        public void StartService(DiscordClient client)
        {
            Thread.Start();
            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Leaderboard service started!");
        }

        public void StopService(DiscordClient client)
        {
            // Request cancellation of the thread
            Cts.Cancel();

            // Wait for the thread to finish
            Thread.Join();

            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Leaderboard service stopped!");
        }

        public void QueueLeaderboardItem(DiscordUser user, DiscordGuild guild)
        {
            Leaderboard.Add(new LeaderboardItem(user, guild));
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

        private class LeaderboardItem
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