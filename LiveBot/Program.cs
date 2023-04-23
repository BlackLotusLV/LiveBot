using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.EventArgs;
using LiveBot.Automation;
using LiveBot.DB;
using LiveBot.Json;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace LiveBot;

internal sealed class Program
{
    private ServiceProvider _provider;
    private static void Main(string[] args)
    {
        Program program = new();
        program.RunBotAsync(args).GetAwaiter().GetResult();
    }

    private async Task RunBotAsync(IEnumerable<string> args)
    {
        var botCredentialsLink = "ConfigFiles/DevBot.json";
        var logLevel = LogLevel.Debug;
        var testBuild = true;
        var databaseConnectionString = "ConfigFiles/DevDatabase.json";
        
        if (args.Any(x=>x.Contains("live")))
        {
            botCredentialsLink = "ConfigFiles/ProdBot.json";
            logLevel = LogLevel.Information;
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
        
        
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddDbContext<LiveBotDbContext>( options =>options.UseNpgsql(dbConnectionString).EnableDetailedErrors(), ServiceLifetime.Transient)
            .AddHttpClient()
            .AddSingleton<ITheCrewHubService,TheCrewHubService>()
            .AddSingleton<IWarningService,WarningService>()
            .AddSingleton<IStreamNotificationService,StreamNotificationService>()
            .AddSingleton<ILeaderboardService,LeaderboardService>()
            .AddSingleton<IModMailService,ModMailService>()
            .BuildServiceProvider();
        _provider = serviceProvider;


        DiscordConfiguration discordConfig = new()
        {
            Token = liveBotSettings.Token,
            TokenType = TokenType.Bot,
            ReconnectIndefinitely = true,
            MinimumLogLevel = logLevel,
            Intents = DiscordIntents.All,
            LogUnknownEvents = false
        };
        CommandsNextConfiguration cNextConfig = new()
        {
            StringPrefixes = new[] { liveBotSettings.CommandPrefix },
            CaseSensitive = false,
            IgnoreExtraArguments = true,
            Services = serviceProvider
        };
        SlashCommandsConfiguration slashCommandConfig = new()
        {
            Services = serviceProvider
        };
        InteractivityConfiguration interactivityConfiguration = new();

        DiscordClient discordClient = new(discordConfig);
        CommandsNextExtension commandsNextExtension = discordClient.UseCommandsNext(cNextConfig);
        SlashCommandsExtension slashCommandsExtension = discordClient.UseSlashCommands(slashCommandConfig);
        discordClient.UseInteractivity(interactivityConfiguration);
        
        discordClient.Ready += Ready;
        discordClient.GuildAvailable += GuildAvailable;
        discordClient.ClientErrored += ClientErrored;

        commandsNextExtension.CommandExecuted += CommandExecuted;
        commandsNextExtension.CommandErrored += CommandErrored;

        slashCommandsExtension.SlashCommandExecuted += SlashExecuted;
        slashCommandsExtension.SlashCommandErrored += SlashErrored;

        slashCommandsExtension.ContextMenuExecuted += ContextMenuExecuted;
        slashCommandsExtension.ContextMenuErrored += ContextMenuErrored;
        
        var memberFlow = ActivatorUtilities.CreateInstance<MemberFlow>(serviceProvider);
        var autoMod = ActivatorUtilities.CreateInstance<AutoMod>(serviceProvider);
        var liveStream = ActivatorUtilities.CreateInstance<LiveStream>(serviceProvider);
        var userActivityTracker = ActivatorUtilities.CreateInstance<UserActivityTracker>(serviceProvider);
        var membershipScreening = ActivatorUtilities.CreateInstance<MembershipScreening>(serviceProvider);
        var whiteListButton = ActivatorUtilities.CreateInstance<WhiteListButton>(serviceProvider);
        var roles = ActivatorUtilities.CreateInstance<Roles>(serviceProvider);
        var getInfractionOnButton = ActivatorUtilities.CreateInstance<GetInfractionOnButton>(serviceProvider);
        var memberKickCheck = ActivatorUtilities.CreateInstance<MemberKickCheck>(serviceProvider);
        
        var warningService = serviceProvider.GetService<IWarningService>();
        var streamNotificationService = serviceProvider.GetService<IStreamNotificationService>();
        var leaderboardService = serviceProvider.GetService<ILeaderboardService>();
        var modMailService = serviceProvider.GetService<IModMailService>();
        var theCrewHubService = serviceProvider.GetService<ITheCrewHubService>();
        
        leaderboardService.StartService(discordClient);
        warningService.StartService(discordClient);
        streamNotificationService.StartService(discordClient);
        await theCrewHubService.StartServiceAsync(discordClient);
        Timer streamCleanupTimer = new(_ => streamNotificationService.StreamListCleanup());
        Timer modMailCleanupTimer = new(async _ => await modMailService.ModMailCleanupAsync(discordClient));
        streamCleanupTimer.Change(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(10));
        modMailCleanupTimer.Change(TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(2));
        
        discordClient.PresenceUpdated += liveStream.Stream_Notification;

        discordClient.MessageCreated += autoMod.Media_Only_Filter;
        discordClient.MessageCreated += autoMod.Spam_Protection;
        discordClient.MessageCreated += autoMod.Link_Spam_Protection;
        discordClient.MessageCreated += autoMod.Everyone_Tag_Protection;
        discordClient.MessageDeleted += autoMod.Delete_Log;
        discordClient.MessagesBulkDeleted += autoMod.Bulk_Delete_Log;
        discordClient.GuildMemberAdded += autoMod.User_Join_Log;
        discordClient.GuildMemberRemoved += autoMod.User_Leave_Log;
        discordClient.GuildMemberRemoved += memberKickCheck.OnRemoved;
        discordClient.GuildBanAdded += autoMod.User_Banned_Log;
        discordClient.GuildBanRemoved += autoMod.User_Unbanned_Log;
        discordClient.VoiceStateUpdated += autoMod.Voice_Activity_Log;
        discordClient.GuildMemberUpdated += autoMod.User_Timed_Out_Log;

        discordClient.ComponentInteractionCreated += getInfractionOnButton.OnPress;

        discordClient.MessageCreated += userActivityTracker.Add_Points;

        discordClient.ComponentInteractionCreated += roles.Button_Roles;
            
        discordClient.ComponentInteractionCreated += whiteListButton.Activate;

        discordClient.GuildMemberAdded += memberFlow.Welcome_Member;
        discordClient.GuildMemberRemoved += memberFlow.Say_Goodbye;

        discordClient.GuildMemberUpdated += membershipScreening.AcceptRules;

        discordClient.MessageCreated += modMailService.ProcessModMailDm;
        discordClient.ComponentInteractionCreated += modMailService.CloseButton;
        discordClient.ComponentInteractionCreated += modMailService.OpenButton;
        
        if (!testBuild)
        {
            discordClient.Logger.LogInformation("Running live version");
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashTheCrewHubCommands>(150283740172517376);
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashModeratorCommands>();
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashCommands>();
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashModMailCommands>();
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashAdministratorCommands>();
        }
        else
        {
            discordClient.Logger.LogInformation("Running in test build mode");
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashTheCrewHubCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashModeratorCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashAdministratorCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashCommands>(282478449539678210);
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashModMailCommands>(282478449539678210);
        }
        
        DiscordActivity botActivity = new($"/send-modmail to open a chat with moderators", ActivityType.Playing);
        await discordClient.ConnectAsync(botActivity);
        await Task.Delay(-1);
    }
    private static Task Ready(DiscordClient client, ReadyEventArgs e)
    {
        client.Logger.LogInformation(CustomLogEvents.LiveBot, "[LiveBot] Client is ready to process events.");
        return Task.CompletedTask;
    }

    private async Task GuildAvailable(DiscordClient client, GuildCreateEventArgs e)
    {
        var dbContext = _provider.GetService<LiveBotDbContext>();
        Guild entry = await dbContext.Guilds.FirstOrDefaultAsync(x => x.Id == e.Guild.Id);
        if (entry==null)
        {
            Guild newEntry = new(e.Guild.Id);
            await dbContext.Guilds.AddAsync(newEntry);
            await dbContext.SaveChangesAsync();
        }

        client.Logger.LogInformation(CustomLogEvents.LiveBot, "Guild available: {GuildName}", e.Guild.Name);
    }
    private static Task ClientErrored(DiscordClient client, ClientErrorEventArgs e)
    {
        client.Logger.LogError(CustomLogEvents.ClientError, e.Exception, "Exception occurred");
        return Task.CompletedTask;
    }

    private static Task CommandExecuted(CommandsNextExtension ext, CommandExecutionEventArgs e)
    {
        ext.Client.Logger.LogInformation("{username} successfully executed '{commandName}' command", e.Context.User.Username,e.Command.QualifiedName);
        return Task.CompletedTask;
    }

    private static async Task CommandErrored(CommandsNextExtension ext, CommandErrorEventArgs e)
    {
        ext.Client.Logger.LogError(CustomLogEvents.CommandError, e.Exception, "{Username} tried executing '{CommandName}' but it errored", e.Context.User.Username, e.Command?.QualifiedName ?? "<unknown command>");
        if (e.Exception is not ChecksFailedException ex) return;
        DiscordEmoji noEntry = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");
        string msgContent = ex.FailedChecks[0] switch
        {
            CooldownAttribute => $"{DiscordEmoji.FromName(e.Context.Client, ":clock:")} You, {e.Context.Member?.Mention}, tried to execute the command too fast, wait and try again later.",
            RequireRolesAttribute => $"{noEntry} You, {e.Context.User.Mention}, don't have the required role for this command",
            RequireDirectMessageAttribute => $"{noEntry} You are trying to use a command that is only available in DMs",
            _ => $"{noEntry} You, {e.Context.User.Mention}, do not have the permissions required to execute this command."
        };
        DiscordEmbedBuilder embed = new()
        {
            Title = "Access denied",
            Description = msgContent,
            Color = new DiscordColor(0xFF0000) // red
        };
        DiscordMessage errorMsg = await e.Context.RespondAsync(string.Empty, embed: embed);
        await Task.Delay(10000);
        await errorMsg.DeleteAsync();
    }

    private static Task SlashExecuted(SlashCommandsExtension ext, SlashCommandExecutedEventArgs e)
    {
        ext.Client.Logger.LogInformation(CustomLogEvents.SlashExecuted, "{Username} successfully executed '{commandName}-{qualifiedName}' command", e.Context.User.Username, e.Context.CommandName, e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    private static Task SlashErrored(SlashCommandsExtension ext, SlashCommandErrorEventArgs e)
    {
        ext.Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}-{qualifiedName}' command, but it errored", e.Context.User.Username, e.Context.CommandName, e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    private static Task ContextMenuExecuted(SlashCommandsExtension ext, ContextMenuExecutedEventArgs e)
    {
        ext.Client.Logger.LogInformation(CustomLogEvents.ContextMenuExecuted, "{Username} Successfully executed '{commandName}-{qualifiedName}' menu command", e.Context.User.Username, e.Context.CommandName,e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    private static Task ContextMenuErrored(SlashCommandsExtension ext, ContextMenuErrorEventArgs e)
    {
        ext.Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}' menu command, but it errored", e.Context.User.Username, e.Context.CommandName);
        return Task.CompletedTask;
    }
}