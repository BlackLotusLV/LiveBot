using System.Reflection.Metadata;
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
using SixLabors.Fonts;
using Microsoft.Extensions.Http;

namespace LiveBot;

internal sealed class Program
{
    private ServiceProvider _provider;
    private static void Main(string[] args)
    {
        Program program = new();
        program.RunBotAsync(args).GetAwaiter().GetResult();
    }

    private async Task RunBotAsync(string[] args)
    {
        var botCredentialsLink = "ConfigFiles/DevBot.Json";
        var logLevel = LogLevel.Debug;
        var testBuild = true;
        
        if (args.Any(x=>x.Contains("live")))
        {
            botCredentialsLink = "ConfigFiles/ProdBot.Json";
            logLevel = LogLevel.Information;
            testBuild = false;
        }

        Bot liveBotSettings;
        string dbConnectionString;
        
        using (StreamReader sr = new(File.OpenRead(botCredentialsLink)))
        {
            string credentialsString = await sr.ReadToEndAsync();
            liveBotSettings = JsonConvert.DeserializeObject<Bot>(credentialsString);
        }

        using (StreamReader sr  = new(File.OpenRead("ConfigFiles/Database.json")))
        {
            string databaseString = await sr.ReadToEndAsync();
            var database = JsonConvert.DeserializeObject<Database>(databaseString);
            dbConnectionString = $"Host={database.Host};Username={database.Username};Password={database.Password};Database={database.Database}; Port={database.Port}";
        }
        
        
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddDbContext<LiveBotDbContext>( options =>options.UseNpgsql(dbConnectionString))
            .AddHttpClient()
            .AddTransient<ITheCrewHubService,TheCrewHubService>()
            .AddTransient<ITheCrewHubService>()
            .AddSingleton<IWarningService,WarningService>()
            .AddSingleton<IStreamNotificationService,StreamNotificationService>()
            .AddSingleton<ILeaderboardService,LeaderboardService>()
            .AddSingleton<IModMailService,ModMailService>()
            .AddLogging()
            .BuildServiceProvider();
        _provider = serviceProvider;

        var warningService = serviceProvider.GetService<IWarningService>();
        var streamNotificationService = serviceProvider.GetService<IStreamNotificationService>();
        var leaderboardService = serviceProvider.GetService<ILeaderboardService>();
        var modMailService = serviceProvider.GetService<IModMailService>();
        var theCrewHubService = serviceProvider.GetService<ITheCrewHubService>();

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
            StringPrefixes = new string[] { liveBotSettings.CommandPrefix },
            CaseSensitive = false,
            IgnoreExtraArguments = true,
            Services = serviceProvider
        };
        SlashCommandsConfiguration slashCommandConfig = new()
        {
            Services = serviceProvider
        };

        DiscordClient discordClient = new(discordConfig);
        CommandsNextExtension commandsNextExtension = discordClient.UseCommandsNext(cNextConfig);
        SlashCommandsExtension slashCommandsExtension = discordClient.UseSlashCommands(slashCommandConfig);
        
        discordClient.Ready += Ready;
        discordClient.GuildAvailable += GuildAvailable;
        discordClient.ClientErrored += ClientErrored;

        commandsNextExtension.CommandExecuted += CommandExecuted;
        commandsNextExtension.CommandErrored += CommandErrored;

        slashCommandsExtension.SlashCommandExecuted += SlashExecuted;
        slashCommandsExtension.SlashCommandErrored += SlashErrored;

        slashCommandsExtension.ContextMenuExecuted += ContextMenuExecuted;
        slashCommandsExtension.ContextMenuErrored += ContextMenuErrored;
        
        leaderboardService.StartService();
        warningService.StartService();
        streamNotificationService.StartService();
        await theCrewHubService.StartServiceAsync();

        Timer timer = new(state => streamNotificationService.StreamListCleanup());
        timer.Change(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(2));
        

        var memberFlow = ActivatorUtilities.CreateInstance<MemberFlow>(serviceProvider);
        var autoMod = ActivatorUtilities.CreateInstance<AutoMod>(serviceProvider);
        var liveStream = ActivatorUtilities.CreateInstance<LiveStream>(serviceProvider);
        var userActivityTracker = ActivatorUtilities.CreateInstance<UserActivityTracker>(serviceProvider);
        var membershipScreening = ActivatorUtilities.CreateInstance<MembershipScreening>(serviceProvider);
        var whiteListButton = ActivatorUtilities.CreateInstance<WhiteListButton>(serviceProvider);
        var roles = ActivatorUtilities.CreateInstance<Roles>(serviceProvider);

        if (!testBuild)
        {
            discordClient.Logger.LogInformation("Running live version");
            
            discordClient.PresenceUpdated += liveStream.Stream_Notification;

            discordClient.MessageCreated += autoMod.Media_Only_Filter;
            discordClient.MessageCreated += autoMod.Spam_Protection;
            discordClient.MessageCreated += autoMod.Link_Spam_Protection;
            discordClient.MessageCreated += autoMod.Everyone_Tag_Protection;
            discordClient.MessageDeleted += autoMod.Delete_Log;
            discordClient.MessagesBulkDeleted += autoMod.Bulk_Delete_Log;
            discordClient.GuildMemberAdded += autoMod.User_Join_Log;
            discordClient.GuildMemberRemoved += autoMod.User_Leave_Log;
            discordClient.GuildMemberRemoved += autoMod.User_Kicked_Log;
            discordClient.GuildBanAdded += autoMod.User_Banned_Log;
            discordClient.GuildBanRemoved += autoMod.User_Unbanned_Log;
            discordClient.VoiceStateUpdated += autoMod.Voice_Activity_Log;
            discordClient.GuildMemberUpdated += autoMod.User_Timed_Out_Log;

            discordClient.MessageCreated += userActivityTracker.Add_Points;

            discordClient.ComponentInteractionCreated += roles.Button_Roles;
            
            discordClient.ComponentInteractionCreated += whiteListButton.Activate;

            discordClient.GuildMemberAdded += memberFlow.Welcome_Member;
            discordClient.GuildMemberRemoved += memberFlow.Say_Goodbye;

            discordClient.GuildMemberUpdated += membershipScreening.AcceptRules;

            discordClient.MessageCreated += modMailService.ProcessModMailDm;
            discordClient.ComponentInteractionCreated += modMailService.CloseButton;
            discordClient.ComponentInteractionCreated += modMailService.OpenButton;

            slashCommandsExtension.RegisterCommands<SlashCommands.SlashTheCrewHubCommands>(150283740172517376);
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashModeratorCommands>();
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashCommands>();
            slashCommandsExtension.RegisterCommands<SlashCommands.SlashModMailCommands>();
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
        await using var dbContext = _provider.GetService<LiveBotDbContext>();
        ServerSettings entry = await dbContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == e.Guild.Id);
        if (entry==null)
        {
            ServerSettings newEntry = new()
            {
                GuildId = e.Guild.Id,
                DeleteLogChannelId = 0,
                UserTrafficChannelId = 0,
                ModerationLogChannelId = 0,
                SpamExceptionChannels = new ulong[] { 0 }
            };
            await dbContext.ServerSettings.AddAsync(newEntry);
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
        await Task.Delay(10000).ContinueWith(t => errorMsg.DeleteAsync());
    }

    private static Task SlashExecuted(SlashCommandsExtension ext, SlashCommandExecutedEventArgs e)
    {
        ext.Client.Logger.LogInformation(CustomLogEvents.SlashExecuted, "{Username} successfully executed '{commandName}-{qualifiedName}' command", e.Context.User.Username, e.Context.CommandName,e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    private static Task SlashErrored(SlashCommandsExtension ext, SlashCommandErrorEventArgs e)
    {
        ext.Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}' but it errored", e.Context.User.Username, e.Context.CommandName ?? "<unknown command>");
        return Task.CompletedTask;
    }

    private static Task ContextMenuExecuted(SlashCommandsExtension ext, ContextMenuExecutedEventArgs e)
    {
        ext.Client.Logger.LogInformation(CustomLogEvents.ContextMenuExecuted, "{Username} Successfully executed '{commandName}-{qualifiedName}' command", e.Context.User.Username, e.Context.CommandName,e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    private static Task ContextMenuErrored(SlashCommandsExtension ext, ContextMenuErrorEventArgs e)
    {
        ext.Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}' but it errored", e.Context.User.Username, e.Context.CommandName ?? "<unknown command>");
        return Task.CompletedTask;
    }
}