using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;
using LiveBot.Automation;
using LiveBot.DB;
using LiveBot.Json;
using LiveBot.LoggerEnrichers;
using LiveBot.Services;
using LiveBot.SlashCommands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;

namespace LiveBot;

internal sealed class Program
{
    private ServiceProvider _serviceProvider;
    private static void Main(string[] args)
    {
        Program program = new();
        program.RunBotAsync(args).GetAwaiter().GetResult();
    }

    private async Task RunBotAsync(IEnumerable<string> args)
    {
        var botCredentialsLink = "ConfigFiles/DevBot.json";
        var logLevel = LogEventLevel.Debug;
        var testBuild = true;
        var databaseConnectionString = "ConfigFiles/DevDatabase.json";
        
        if (args.Any(x=>x.Contains("live")))
        {
            botCredentialsLink = "ConfigFiles/ProdBot.json";
            logLevel = LogEventLevel.Information;
            testBuild = false;
            databaseConnectionString = "ConfigFiles/Database.json";
        }

        Bot liveBotSettings;
        string dbConnectionString;
        
        using (StreamReader sr = new(File.OpenRead(botCredentialsLink)))
        {
            string credentialsString = await sr.ReadToEndAsync();
            liveBotSettings = JsonConvert.DeserializeObject<Bot>(credentialsString);
        }

        using (StreamReader sr  = new(File.OpenRead(databaseConnectionString)))
        {
            string databaseString = await sr.ReadToEndAsync();
            var database = JsonConvert.DeserializeObject<DatabaseJson>(databaseString);
            dbConnectionString = $"Host={database.Host};Username={database.Username};Password={database.Password};Database={database.Database};Port={database.Port}";
        }

        Log.Logger = new LoggerConfiguration()
            .Enrich.With(new EventIdEnricher())
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .WriteTo.Console( standardErrorFromLevel: LogEventLevel.Error ,outputTemplate:"[{Timestamp:yyyy:MM:dd HH:mm:ss} {Level:u3}] [{FormattedEventId}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        ILoggerFactory loggerFactory = new LoggerFactory().AddSerilog();
        
        _serviceProvider = new ServiceCollection()
            .AddPooledDbContextFactory<LiveBotDbContext>(options=>options.UseNpgsql(dbConnectionString).EnableDetailedErrors())
            .AddHttpClient()
            .AddSingleton<IDatabaseMethodService, DatabaseMethodService>()
            .AddSingleton<ITheCrewHubService,TheCrewHubService>()
            .AddSingleton<IWarningService,WarningService>()
            .AddSingleton<IStreamNotificationService,StreamNotificationService>()
            .AddSingleton<ILeaderboardService,LeaderboardService>()
            .AddSingleton<IModMailService,ModMailService>()
            .AddSingleton<IModLogService,ModLogService>()
            .AddSingleton(loggerFactory)
            .BuildServiceProvider();
        
        DiscordConfiguration discordConfig = new()
        {
            Token = liveBotSettings.Token,
            TokenType = TokenType.Bot,
            ReconnectIndefinitely = true,
            Intents = DiscordIntents.All,
            LogUnknownEvents = false,
            LoggerFactory = loggerFactory
        };
        CommandsNextConfiguration cNextConfig = new()
        {
            StringPrefixes = new[] { liveBotSettings.CommandPrefix },
            CaseSensitive = false,
            IgnoreExtraArguments = true,
            Services = _serviceProvider
        };
        SlashCommandsConfiguration slashCommandConfig = new()
        {
            Services = _serviceProvider
        };
        InteractivityConfiguration interactivityConfiguration = new();
        
        DiscordClient discordClient = new(discordConfig);
        CommandsNextExtension commandsNextExtension = discordClient.UseCommandsNext(cNextConfig);
        SlashCommandsExtension slashCommandsExtension = discordClient.UseSlashCommands(slashCommandConfig);
        discordClient.UseInteractivity(interactivityConfiguration);
        
        var memberFlow = ActivatorUtilities.CreateInstance<MemberFlow>(_serviceProvider);
        var autoMod = ActivatorUtilities.CreateInstance<AutoMod>(_serviceProvider);
        var liveStream = ActivatorUtilities.CreateInstance<LiveStream>(_serviceProvider);
        var userActivityTracker = ActivatorUtilities.CreateInstance<UserActivityTracker>(_serviceProvider);
        var membershipScreening = ActivatorUtilities.CreateInstance<MembershipScreening>(_serviceProvider);
        var whiteListButton = ActivatorUtilities.CreateInstance<WhiteListButton>(_serviceProvider);
        var roles = ActivatorUtilities.CreateInstance<Roles>(_serviceProvider);
        var getInfractionOnButton = ActivatorUtilities.CreateInstance<GetInfractionOnButton>(_serviceProvider);
        var getUserInfoOnButton = ActivatorUtilities.CreateInstance<GetUserInfoOnButton>(_serviceProvider);
        var auditLogManager = ActivatorUtilities.CreateInstance<AuditLogManager>(_serviceProvider);
        var duplicateMessageCatcher = ActivatorUtilities.CreateInstance<DuplicateMessageCatcher>(_serviceProvider);
        var systemEventMethods = ActivatorUtilities.CreateInstance<SystemEventMethods>(_serviceProvider);
        
        var warningService = _serviceProvider.GetService<IWarningService>();
        var streamNotificationService = _serviceProvider.GetService<IStreamNotificationService>();
        var leaderboardService = _serviceProvider.GetService<ILeaderboardService>();
        var modMailService = _serviceProvider.GetService<IModMailService>();
        var theCrewHubService = _serviceProvider.GetService<ITheCrewHubService>();
        var modLogService = _serviceProvider.GetService<IModLogService>();
        
        leaderboardService.StartService(discordClient);
        warningService.StartService(discordClient);
        streamNotificationService.StartService(discordClient);
        await theCrewHubService.StartServiceAsync();
        modLogService.StartService(discordClient);
        
        Timer streamCleanupTimer = new(_ => streamNotificationService.StreamListCleanup());
        Timer modMailCleanupTimer = new(_ =>  modMailService.ModMailCleanupAsync(discordClient));
        streamCleanupTimer.Change(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(10));
        modMailCleanupTimer.Change(TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(2));
        
        discordClient.SessionCreated += systemEventMethods.SessionCreated;
        discordClient.GuildAvailable += systemEventMethods.GuildAvailable;
        discordClient.ClientErrored += systemEventMethods.ClientErrored;

        commandsNextExtension.CommandExecuted += systemEventMethods.CommandExecuted;
        commandsNextExtension.CommandErrored += systemEventMethods.CommandErrored;

        slashCommandsExtension.SlashCommandExecuted += systemEventMethods.SlashExecuted;
        slashCommandsExtension.SlashCommandErrored += systemEventMethods.SlashErrored;

        slashCommandsExtension.ContextMenuExecuted += systemEventMethods.ContextMenuExecuted;
        slashCommandsExtension.ContextMenuErrored += systemEventMethods.ContextMenuErrored;
        
        discordClient.PresenceUpdated += liveStream.Stream_Notification;

        discordClient.MessageCreated += autoMod.Media_Only_Filter;
        discordClient.MessageCreated += autoMod.Link_Spam_Protection;
        discordClient.MessageCreated += autoMod.Everyone_Tag_Protection;
        discordClient.MessageDeleted += autoMod.Delete_Log;
        discordClient.MessagesBulkDeleted += autoMod.Bulk_Delete_Log;
        discordClient.GuildMemberAdded += autoMod.User_Join_Log;
        discordClient.GuildMemberRemoved += autoMod.User_Leave_Log;
        discordClient.VoiceStateUpdated += autoMod.Voice_Activity_Log;
        
        discordClient.MessageCreated += duplicateMessageCatcher.CheckMessage;

        discordClient.ComponentInteractionCreated += getInfractionOnButton.OnPress;
        discordClient.ComponentInteractionCreated += getUserInfoOnButton.OnPress;

        discordClient.MessageCreated += userActivityTracker.Add_Points;

        discordClient.ComponentInteractionCreated += roles.Button_Roles;
            
        discordClient.ComponentInteractionCreated += whiteListButton.Activate;

        discordClient.GuildMemberAdded += memberFlow.Welcome_Member;
        discordClient.GuildMemberRemoved += memberFlow.Say_Goodbye;

        discordClient.GuildMemberUpdated += membershipScreening.AcceptRules;

        discordClient.MessageCreated += modMailService.ProcessModMailDm;
        discordClient.ComponentInteractionCreated += modMailService.CloseButton;
        discordClient.ComponentInteractionCreated += modMailService.OpenButton;

        discordClient.UnknownEvent += auditLogManager.UnknownEventToAuditLog;
        
        if (!testBuild)
        {
            discordClient.Logger.LogInformation(CustomLogEvents.LiveBot,"Running live version");
            slashCommandsExtension.RegisterCommands<SlashTheCrewHubCommands>(150283740172517376);
            slashCommandsExtension.RegisterCommands<SlashModeratorCommands>();
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashCommands>();
            slashCommandsExtension.RegisterCommands<SlashModMailCommands>();
            slashCommandsExtension.RegisterCommands<SlashAdministratorCommands>();
        }
        else
        {
            discordClient.Logger.LogInformation(CustomLogEvents.LiveBot,"Running in test build mode");
            slashCommandsExtension.RegisterCommands<SlashTheCrewHubCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashModeratorCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashAdministratorCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashModMailCommands>(282478449539678210);
        }
        
        DiscordActivity botActivity = new("/send-modmail to open a chat with moderators", ActivityType.Playing);
        await discordClient.ConnectAsync(botActivity);
        await Task.Delay(-1);
    }
}