using LiveBot.DB;

namespace LiveBot.Services
{
    public interface ILeaderboardService
    {
        void StartService(DiscordClient client);
        void StopService();
        void AddToQueue(LeaderboardService.LeaderboardItem value);
    }
    public sealed class LeaderboardService : BaseQueueService<LeaderboardService.LeaderboardItem>, ILeaderboardService
    {
        public LeaderboardService(IDbContextFactory dbContextFactory, IDatabaseMethodService databaseMethodService) : base(dbContextFactory, databaseMethodService) {}

        private protected override async Task ProcessQueueAsync()
        {
            foreach (LeaderboardItem value in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                try
                {
                    await _databaseMethodService.AddGuildUsersAsync(new GuildUser(value.User.Id, value.Guild.Id));
                }
                catch (Exception e)
                {
                    _client.Logger.LogError("{} failed to process item in queue/n{Error} ", this.GetType().Name,e.Message);
                }
            }
        }

        public class LeaderboardItem
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