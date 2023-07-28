using LiveBot.DB;

namespace LiveBot.Services;

public interface ILeaderboardService
{
    void StartService(DiscordClient client);
    void StopService();
    void AddToQueue(LeaderboardService.LeaderboardItem value);
}

public sealed class LeaderboardService : BaseQueueService<LeaderboardService.LeaderboardItem>, ILeaderboardService
{
    public LeaderboardService(IDbContextFactory dbContextFactory, IDatabaseMethodService databaseMethodService, ILoggerFactory loggerFactory) : base(dbContextFactory, databaseMethodService,
        loggerFactory)
    {
    }

    private protected override async Task ProcessQueueAsync()
    {
        foreach (LeaderboardItem value in Queue.GetConsumingEnumerable(CancellationTokenSource.Token))
        {
            try
            {
                await DatabaseMethodService.AddGuildUsersAsync(new GuildUser(value.User.Id, value.Guild.Id));
            }
            catch (Exception e)
            {
                Logger.LogError(CustomLogEvents.ServiceError, e, "{} failed to process item in queue", GetType().Name);
            }
        }
    }

    public class LeaderboardItem
    {
        public DiscordGuild Guild { get; set; }
        public DiscordUser User { get; set; }

        public LeaderboardItem(DiscordUser user, DiscordGuild guild)
        {
            Guild = guild;
            User = user;
        }
    }
}