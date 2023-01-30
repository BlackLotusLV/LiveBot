using System.Collections.Concurrent;
using LiveBot.DB;

namespace LiveBot.Services;

public abstract class BaseQueueService<T>
{
    private protected ILogger _logger;
    protected readonly LiveBotDbContext _databaseContext;
    private protected readonly CancellationTokenSource _cancellationTokenSource;
    private Task _backgroundTask;
    private readonly Type _type;
    private protected BlockingCollection<T> _queue = new();

    protected BaseQueueService(ILogger logger, LiveBotDbContext databaseContext)
    {
        _logger = logger;
        _databaseContext = databaseContext;
        _cancellationTokenSource = new CancellationTokenSource();
        _type = this.GetType();
    }

    public void StartService()
    {
        _logger.LogInformation("{Type} service starting!",_type.Name);
        _backgroundTask = ProcessQueueAsync();
        _logger.LogInformation("{Type} service has started!",_type.Name);
    }
    public void StopService()
    {
        _logger.LogInformation("{Type} service stopping!",_type.Name);
        _cancellationTokenSource.Cancel();
        _logger.LogInformation("{Type} service has stopped!",_type.Name);
    }

    private protected abstract Task ProcessQueueAsync();

    public void AddToQueue(T value)
    {
        _queue.Add(value);
    }
}