using System.Collections.Immutable;
using System.Text.RegularExpressions;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

public partial class InviteLinkFilter
{
    private readonly IWarningService _warningService;
    private readonly IDbContextFactory<LiveBotDbContext> _dbContextFactory;

    public InviteLinkFilter(IWarningService warningService, IDbContextFactory<LiveBotDbContext> dbContextFactory)
    {
        _warningService = warningService;
        _dbContextFactory = dbContextFactory;
    }
    
    public async Task OnMessageSend(DiscordClient client, MessageCreateEventArgs eventArgs)
    {
        if (eventArgs.Author.IsBot || eventArgs.Guild is null || !InviteRegex().IsMatch(eventArgs.Message.Content)) return;
        client.Logger.LogDebug(CustomLogEvents.InviteLinkFilter, "Invite link detected in {GuildName}({GuildId}) by {Username}({UserId})",
            eventArgs.Guild.Name, eventArgs.Guild.Id, eventArgs.Author.Username, eventArgs.Author.Id);
        DiscordMember member = await eventArgs.Guild.GetMemberAsync(eventArgs.Author.Id);
        if (CustomMethod.CheckIfMemberAdmin(member)) return;
        
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guild = await liveBotDbContext.Guilds.Include(x=>x.WhitelistedVanities).FirstOrDefaultAsync(x=>x.Id==eventArgs.Guild.Id);
        if (guild is null || !guild.HasLinkProtection || guild.ModerationLogChannelId is null) return;
        var guildInvites = await eventArgs.Guild.GetInvitesAsync();

        var matches = InviteRegex().Matches(eventArgs.Message.Content).Select(x=>x.Value).ToImmutableList();
        if (matches.Any(match => 
                guildInvites.Any(x=>Regex.IsMatch(match, $"/{x.Code}(\\s|$)")) ||
                (eventArgs.Guild.VanityUrlCode is not null && Regex.IsMatch(match, $"/{eventArgs.Guild.VanityUrlCode}(\\s|$)"))|
                guild.WhitelistedVanities.Any(x => Regex.IsMatch(match, $"/{x.VanityCode}(\\s|$)")))) 
            return;

        await eventArgs.Message.DeleteAsync("Invite link detected");
        await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1), "Spam protection triggered - invite links");
        _warningService.AddToQueue(new WarningItem(eventArgs.Author, client.CurrentUser, eventArgs.Guild, eventArgs.Channel, "Spam protection triggered - invite links", true));
        client.Logger.LogInformation("User {Username}({UserId}) tried to post an invite link in {GuildName}({GuildId})",
            eventArgs.Author.Username, eventArgs.Author.Id, eventArgs.Guild.Name, eventArgs.Guild.Id);

    }

    [GeneratedRegex(@"(https?:\/\/)?(www\.)?(discord\.(gg|me)|discordapp\.com\/invite)\/\w{1,}(\?event=\d{1,})?")]
    private static partial Regex InviteRegex();
}