using LiveBot.DB;

namespace LiveBot.Services
{
    public interface ILeaderboardService
    {
        void StartService();
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
                await _databaseContext.GuildUsers.AddAsync(new GuildUser(_databaseContext, value.User.Id, value.Guild.Id));
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