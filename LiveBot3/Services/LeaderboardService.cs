using LiveBot.DB;

namespace LiveBot.Services
{
    public interface ILeaderboardService
    {
        void StartService();
        void StopService();
        void AddToQueue(LeaderboardService.LeaderboardItem value);
    }
    public abstract class LeaderboardService : BaseQueueService<LeaderboardService.LeaderboardItem>, ILeaderboardService
    {
        LeaderboardService(ILogger logger,LiveBotDbContext dbContext) : base(logger,dbContext){}

        private protected override async Task ProcessQueueAsync()
        {
            foreach (LeaderboardItem value in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                await _databaseContext.ServerRanks.AddAsync(new ServerRanks(_databaseContext, value.User.Id, value.Guild.Id));
                await _databaseContext.SaveChangesAsync();
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