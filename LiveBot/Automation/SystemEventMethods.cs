using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.EventArgs;
using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation;

public sealed class SystemEventMethods
{
    private readonly IDatabaseMethodService _databaseMethodService;
    private readonly IDbContextFactory _dbContextFactory;
    public SystemEventMethods(IDatabaseMethodService databaseMethodService, IDbContextFactory dbContextFactory)
    {
        _databaseMethodService = databaseMethodService;
        _dbContextFactory = dbContextFactory;
    }
    
    public Task SessionCreated(DiscordClient client, SessionReadyEventArgs e)
    {
        client.Logger.LogInformation(CustomLogEvents.LiveBot, "Client is ready to process events");
        return Task.CompletedTask;
    }

    public async Task GuildAvailable(DiscordClient client, GuildCreateEventArgs e)
    {
        await using LiveBotDbContext dbContext = _dbContextFactory.CreateDbContext();
        _ = await dbContext.Guilds.FindAsync(e.Guild.Id) ?? await _databaseMethodService.AddGuildAsync(new Guild(e.Guild.Id));
        client.Logger.LogInformation(CustomLogEvents.LiveBot, "Guild available: {GuildName}", e.Guild.Name);
    }
    public Task ClientErrored(DiscordClient client, ClientErrorEventArgs e)
    {
        client.Logger.LogError(CustomLogEvents.ClientError, e.Exception, "Exception occurred");
        return Task.CompletedTask;
    }

    public Task CommandExecuted(CommandsNextExtension ext, CommandExecutionEventArgs e)
    {
        ext.Client.Logger.LogInformation("{Username} successfully executed '{CommandName}' command", e.Context.User.Username,e.Command.QualifiedName);
        return Task.CompletedTask;
    }

    public async Task CommandErrored(CommandsNextExtension ext, CommandErrorEventArgs e)
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

    public Task SlashExecuted(SlashCommandsExtension ext, SlashCommandExecutedEventArgs e)
    {
        ext.Client.Logger.LogInformation(CustomLogEvents.SlashExecuted, "{Username} successfully executed '{CommandName}-{QualifiedName}' command", e.Context.User.Username, e.Context.CommandName, e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    public Task SlashErrored(SlashCommandsExtension ext, SlashCommandErrorEventArgs e)
    {
        ext.Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}-{QualifiedName}' command, but it errored", e.Context.User.Username, e.Context.CommandName, e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    public Task ContextMenuExecuted(SlashCommandsExtension ext, ContextMenuExecutedEventArgs e)
    {
        ext.Client.Logger.LogInformation(CustomLogEvents.ContextMenuExecuted, "{Username} Successfully executed '{CommandName}-{QualifiedName}' menu command", e.Context.User.Username, e.Context.CommandName,e.Context.QualifiedName);
        return Task.CompletedTask;
    }

    public Task ContextMenuErrored(SlashCommandsExtension ext, ContextMenuErrorEventArgs e)
    {
        ext.Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}' menu command, but it errored", e.Context.User.Username, e.Context.CommandName);
        return Task.CompletedTask;
    }
}