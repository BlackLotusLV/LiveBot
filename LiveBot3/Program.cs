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

namespace LiveBot;

internal sealed class Program
{
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
            .BuildServiceProvider();

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
        SlashCommandsExtension slashCommandsExtension = discordClient.UseSlashCommands(slashCommandConfig);

        discordClient.Ready += Ready;
        discordClient.GuildAvailable += GuildAvailable;
        discordClient.ClientErrored += ClientErrored;

        if (!testBuild)
        {
            discordClient.Logger.LogInformation("Running live version");

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
    private Task Ready(DiscordClient client, ReadyEventArgs e)
    {
        client.Logger.LogInformation(CustomLogEvents.LiveBot, "[LiveBot] Client is ready to process events.");
        return Task.CompletedTask;
    }

    private Task GuildAvailable(DiscordClient client, GuildCreateEventArgs e)
    {
        client.Logger.LogInformation(CustomLogEvents.LiveBot, "Guild available: {GuildName}", e.Guild.Name);
        return Task.CompletedTask;
    }
    private Task ClientErrored(DiscordClient client, ClientErrorEventArgs e)
    {
        client.Logger.LogError(CustomLogEvents.ClientError, e.Exception, "Exception occurred");
        return Task.CompletedTask;
    }
}