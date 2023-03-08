using LiveBot.DB;

namespace LiveBot.Services
{
    public interface ILeaderboardService
    {
        void StartService(DiscordClient client);
        void StopService();
        void AddToQueue(LeaderboardService.LeaderboardItem value);
    }
    public class LeaderboardService : BaseQueueService<LeaderboardService.LeaderboardItem>, ILeaderboardService
    {
        public LeaderboardService(LiveBotDbContext dbContext) : base(dbContext){}

        private protected override async Task ProcessQueueAsync()
        {
            foreach (LeaderboardItem value in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                try
                {
                    await _databaseContext.AddGuildUsersAsync(_databaseContext, new GuildUser(value.User.Id, value.Guild.Id));
                }
                catch (Exception e)
                {
                    _client.Logger.LogError("{} failed to process item in queue ", this.GetType().Name);
                    continue;
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