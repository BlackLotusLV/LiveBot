using System.Collections.Concurrent;
using LiveBot.DB;

namespace LiveBot.Services;

public abstract class BaseQueueService<T>
{
    protected readonly LiveBotDbContext _databaseContext;
    private protected readonly CancellationTokenSource _cancellationTokenSource;
    private Task _backgroundTask;
    private readonly Type _type;
    private protected BlockingCollection<T> _queue = new();
    private protected DiscordClient _client;

    protected BaseQueueService(LiveBotDbContext databaseContext)
    {
        _databaseContext = databaseContext;
        _cancellationTokenSource = new CancellationTokenSource();
        _type = this.GetType();
    }

    public void StartService(DiscordClient client)
    {
        _client = client;
        _client.Logger.LogInformation("{Type} service starting!",_type.Name);
        _backgroundTask = Task.Run(async ()=>await ProcessQueueAsync(),_cancellationTokenSource.Token);
        _client.Logger.LogInformation("{Type} service has started!",_type.Name);
    }
    public void StopService()
    {
        _client.Logger.LogInformation("{Type} service stopping!",_type.Name);
        _cancellationTokenSource.Cancel();
        _backgroundTask.Wait();
        _client.Logger.LogInformation("{Type} service has stopped!",_type.Name);
    }

    private protected abstract Task ProcessQueueAsync();

    public void AddToQueue(T value)
    {
        _queue.Add(value);
    }
}