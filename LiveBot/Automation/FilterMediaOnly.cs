using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

public class FilterMediaOnly
{
    private readonly IDbContextFactory<LiveBotDbContext> _contextFactory;
    public FilterMediaOnly(IDbContextFactory<LiveBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }
    public async Task OnMessageSent(DiscordClient client, MessageCreateEventArgs args)
    {
        LiveBotDbContext context = await _contextFactory.CreateDbContextAsync();
        Guild guild = await context.Guilds.Include(x => x.MediaOnlyChannels).FirstOrDefaultAsync(x => x.Id == args.Guild.Id);
        if (guild is null)
        {
            await context.AddAsync(new Guild(args.Guild.Id));
            await context.SaveChangesAsync();
            return;
        }
        if (guild.MediaOnlyChannels.Count == 0 || args.Author.IsBot || args.Message.Attachments.Count!=0 || args.Message.Content.Split(' ').Any(x=>Uri.TryCreate(x, UriKind.Absolute, out _))) return;
        MediaOnlyChannels channel = guild.MediaOnlyChannels.FirstOrDefault(x => x.ChannelId == args.Channel.Id);
        if (channel is null) return;
        await args.Message.DeleteAsync();
        string response = channel.ResponseMessage ?? "This channel is for sharing media only, please use the content comment channel for discussions. If this is a mistake please contact a moderator.";
        
        DiscordMessage msg = await args.Channel.SendMessageAsync(response);
        await Task.Delay(9000);
        await msg.DeleteAsync();
        client.Logger.LogInformation(CustomLogEvents.PhotoCleanup,
            "User {Username}({UserId}) tried to send text in a media only channel. Message deleted", args.Author.Username, args.Author.Id);
    }
}