using LiveBot.DB;
using Microsoft.Extensions.DependencyInjection;

namespace LiveBot.Services;

public interface IDbContextFactory
{
    public LiveBotDbContext CreateDbContext();
}
public class DbContextFactory : IDbContextFactory
{
    private readonly IServiceProvider _serviceProvider;
    public DbContextFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    public LiveBotDbContext CreateDbContext()
    {
        return _serviceProvider.GetRequiredService<LiveBotDbContext>();
    }
}