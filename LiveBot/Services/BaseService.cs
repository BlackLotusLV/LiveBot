using System.Collections.Concurrent;
using LiveBot.DB;

namespace LiveBot.Services;

public abstract class BaseQueueService<T>
{
    private protected readonly IDbContextFactory _dbContextFactory;
    private protected readonly IDatabaseMethodService _databaseMethodService;
    private protected readonly CancellationTokenSource _cancellationTokenSource;
    private Task _backgroundTask;
    private readonly Type _type;
    private protected BlockingCollection<T> _queue = new();
    private protected ILogger<T> _logger;
    private DiscordClient Client;

    protected BaseQueueService(IDbContextFactory dbContextFactory, IDatabaseMethodService databaseMethodService, ILoggerFactory loggerFactory)
    {
        _dbContextFactory = dbContextFactory;
        _databaseMethodService = databaseMethodService;
        _cancellationTokenSource = new CancellationTokenSource();
        _type = this.GetType();
        _logger = loggerFactory.CreateLogger<T>();
    }

    public void StartService(DiscordClient client)
    {
        Client=client;
        _logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service starting!",_type.Name);
        _backgroundTask = Task.Run(async ()=>await ProcessQueueAsync(),_cancellationTokenSource.Token);
        _logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service has started!",_type.Name);
    }
    public void StopService()
    {
        _logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service stopping!",_type.Name);
        _cancellationTokenSource.Cancel();
        _backgroundTask.Wait();
        _logger.LogInformation(CustomLogEvents.LiveBot,"{Type} service has stopped!",_type.Name);
    }

    private protected abstract Task ProcessQueueAsync();

    public void AddToQueue(T value)
    {
        _queue.Add(value);
    }
    public DiscordUser GetBotUser()
    {
        return Client.CurrentUser;
    }
}