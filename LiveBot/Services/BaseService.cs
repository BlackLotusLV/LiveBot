using System.Collections.Concurrent;
using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Services;

public abstract class BaseQueueService<T>
{
    private protected readonly IDbContextFactory<LiveBotDbContext> DbContextFactory;
    private protected readonly IDatabaseMethodService DatabaseMethodService;
    private protected readonly CancellationTokenSource CancellationTokenSource;
    private Task _backgroundTask;
    private readonly Type _type;
    private protected readonly BlockingCollection<T> Queue = new();
    private protected readonly ILogger<T> Logger;
    private DiscordClient _client;

    protected BaseQueueService(IDbContextFactory<LiveBotDbContext> dbContextFactory, IDatabaseMethodService databaseMethodService, ILoggerFactory loggerFactory)
    {
        DbContextFactory = dbContextFactory;
        DatabaseMethodService = databaseMethodService;
        CancellationTokenSource = new CancellationTokenSource();
        _type = GetType();
        Logger = loggerFactory.CreateLogger<T>();
    }

    public void StartService(DiscordClient client)
    {
        _client=client;
        Logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service starting!",_type.Name);
        _backgroundTask = Task.Run(async ()=>await ProcessQueueAsync(),CancellationTokenSource.Token);
        Logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service has started!",_type.Name);
    }
    public void StopService()
    {
        Logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service stopping!",_type.Name);
        CancellationTokenSource.Cancel();
        _backgroundTask.Wait();
        Logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service has stopped!",_type.Name);
    }

    private protected abstract Task ProcessQueueAsync();

    public void AddToQueue(T value)
    {
        Queue.Add(value);
    }

    protected DiscordUser GetBotUser()
    {
        return _client.CurrentUser;
    }
}