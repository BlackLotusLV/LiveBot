using System.Collections.Concurrent;
using System.Collections.Immutable;
using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation;

public class DuplicateMessageCatcher
{
    private readonly IWarningService _warningService;
    private readonly DbContextFactory _dbContextFactory;
    private readonly IModLogService _modLogService;
    private const int SpamInterval = 6;
    private const int SpamCount = 5;
    private readonly List<DiscordMessage> _messageList = new();

    public DuplicateMessageCatcher(IWarningService warningService, DbContextFactory dbContextFactory, IModLogService modLogService)
    {
        _warningService = warningService;
        _dbContextFactory = dbContextFactory;
        _modLogService = modLogService;
    }
    public async Task CheckMessage(DiscordClient client, MessageCreateEventArgs eventArgs)
    {
        if (eventArgs.Author.IsBot || eventArgs.Author.IsCurrent || eventArgs.Guild is null) return;
        LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
        Guild guild = await liveBotDbContext.Guilds.FindAsync(eventArgs.Guild.Id);
        if (guild is null) return;
        var spamIgnoreChannels =
            liveBotDbContext.SpamIgnoreChannels.AsQueryable().Where(x => x.GuildId == eventArgs.Guild.Id).ToImmutableArray();

        if (guild?.ModerationLogChannelId == null || spamIgnoreChannels.Any(x=>x.ChannelId==eventArgs.Channel.Id)) return;
        DiscordMember member = await eventArgs.Guild.GetMemberAsync(eventArgs.Author.Id);

        if (CustomMethod.CheckIfMemberAdmin(member)) return;
        _messageList.Add(eventArgs.Message);
        var duplicateMessages = _messageList.Where(w => w.Author == eventArgs.Author && w.Content == eventArgs.Message.Content && eventArgs.Guild == w.Channel.Guild).ToList();
        int i = duplicateMessages.Count;
        if (i < SpamCount) return;

        TimeSpan time = (duplicateMessages[i - 1].CreationTimestamp - duplicateMessages[i - SpamCount].CreationTimestamp) / SpamCount;
        if (time >= TimeSpan.FromSeconds(SpamInterval)) return;
            
        var channelList = duplicateMessages.GetRange(i - SpamCount, SpamCount).Select(s => s.Channel).Distinct().ToList();
        await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1), "Spam filter triggered - flood");
        foreach (DiscordChannel channel in channelList)
        {
            await channel.DeleteMessagesAsync(duplicateMessages.GetRange(i - SpamCount, SpamCount));
        }

        int infractionLevel = liveBotDbContext.Infractions.Count(w => w.UserId == member.Id && w.GuildId == eventArgs.Guild.Id && w.InfractionType == InfractionType.Warning && w.IsActive);

        if (infractionLevel < 5)
        {
            _warningService.AddToQueue(new WarningItem(eventArgs.Author, client.CurrentUser, eventArgs.Guild, eventArgs.Channel, "Spam protection triggered - flood", true));
        }
    }
}