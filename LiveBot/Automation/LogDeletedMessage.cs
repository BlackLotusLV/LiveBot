using System.Net.Http;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

public class LogDeletedMessage
{
    private readonly IWarningService _warningService;
    private readonly IModLogService _modLogService;
    private readonly IDbContextFactory<LiveBotDbContext> _dbContextFactory;
    private readonly IDatabaseMethodService _databaseMethodService;
    private readonly HttpClient _httpClient;
    
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 4096;
    private const int MaxFields = 25;
    private const int MaxFieldNameLength = 256;
    private const int MaxFieldValueLength = 1024;
    private const int MaxFooterLength = 2048;
    private const int MaxEmbedLength = 6000;
    private readonly List<string> _imageExtensions = new() { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tiff", ".jfif", ".svg", ".ico" };

    public LogDeletedMessage(IWarningService warningService, IModLogService modLogService, IDbContextFactory<LiveBotDbContext> dbContextFactory, IDatabaseMethodService databaseMethodService, HttpClient httpClient)
    {
        _warningService = warningService;
        _modLogService = modLogService;
        _dbContextFactory = dbContextFactory;
        _databaseMethodService = databaseMethodService;
        _httpClient = httpClient;
    }

    public async Task OnMessageDeleted(DiscordClient client, MessageDeleteEventArgs args)
    {
        if (args.Guild is null) return;
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guild = await liveBotDbContext.Guilds.FindAsync(args.Guild.Id) ?? await _databaseMethodService.AddGuildAsync(new Guild(args.Guild.Id));
        if (guild.DeleteLogChannelId is null) return;
        DiscordChannel deleteLogChannel = client.Guilds.FirstOrDefault(w => w.Value.Id == guild.Id).Value.GetChannel(guild.DeleteLogChannelId.Value);
        
        if (args.Message.Author is null)
        {
            DiscordEmbedBuilder embed = new()
            {
                Color = new DiscordColor(0xFF6600),
                Description = $"# Message Deleted\n" +
                              $"- **Author:**  UNKNOWN\n" +
                              $"- **Channel:** {args.Channel.Mention}\n" +
                              $"*Uncached message deleted, no info found. Logging as much as possible.*"
            };
            DiscordMessageBuilder message = new();
            message.AddEmbed(embed.Build());
            await deleteLogChannel.SendMessageAsync(message);
            client.Logger.LogInformation(CustomLogEvents.DeleteLog, "Uncached message deleted in {Channel}", args.Channel.Name);
            return;
        }
        if (args.Message.Author.IsBot) return;
        string msgContent = args.Message.Content==""?"*message didn't contain any text*":$"*{args.Message.Content}*";
        StringBuilder sb = new();
        sb.Append($"# Message Deleted\n" +
                  $"- **Author:** {args.Message.Author.Mention}({args.Message.Author.Mention})\n" +
                  $"- **Channel:** {args.Channel.Mention}\n" +
                  $"- **Attachment Count:** {args.Message.Attachments.Count}\n" +
                  $"- **Time posted:** <t:{args.Message.CreationTimestamp.ToUnixTimeSeconds()}:F>\n" +
                  $"- **Message:** ");
        if (sb.ToString().Length + msgContent.Length > MaxDescriptionLength)
        {
            sb.Append($"{msgContent.AsSpan(0, MaxDescriptionLength - (sb.ToString().Length+3))}...");
        }
        else
        {
            sb.Append($"{msgContent}");
        }

        DiscordEmbedBuilder embedBuilder = new()
        {
            Color = new DiscordColor(0xFF6600),
            Description = sb.ToString(),
            Author = new DiscordEmbedBuilder.EmbedAuthor
            {
                IconUrl = args.Message.Author.AvatarUrl,
                Name = $"{args.Message.Author.Username}'s message deleted"
            }
        };
        DiscordMessageBuilder messageBuilder = new();
        List<DiscordEmbed> attachmentEmbeds = new();
        List<MemoryStream> imageStreams = new();
        StringBuilder attachmentNames = new();
        foreach (DiscordAttachment messageAttachment in args.Message.Attachments)
        {
            attachmentNames.AppendLine($"- {messageAttachment.FileName}");
            if (!_imageExtensions.Contains(Path.GetExtension(messageAttachment.FileName))) continue;
            HttpResponseMessage response = await _httpClient.GetAsync(messageAttachment.Url);
            if (!response.IsSuccessStatusCode) continue;
            var uniqueFileName = $"{Guid.NewGuid()}-{messageAttachment.FileName}";
            MemoryStream ms = new();
            await response.Content.CopyToAsync(ms);
            ms.Position = 0;
            messageBuilder.AddFile(uniqueFileName, ms);
            imageStreams.Add(ms);
            if (embedBuilder.ImageUrl is null)
            {
                embedBuilder.ImageUrl = $"attachment://{uniqueFileName}";
            }
            else
            {
                attachmentEmbeds.Add(new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor(0xFF6600),
                        ImageUrl = $"attachment://{uniqueFileName}"
                    }.Build()
                );
            }
        }

        if (args.Message.Attachments.Count!=0)
        {
            embedBuilder.AddField("Attachment Names", attachmentNames.ToString());
        }

        attachmentEmbeds.Insert(0, embedBuilder.Build());
        messageBuilder.AddEmbeds(attachmentEmbeds);

        await deleteLogChannel.SendMessageAsync(messageBuilder);
        await Parallel.ForEachAsync(imageStreams.AsEnumerable(), async (MemoryStream stream, CancellationToken _) => await stream.DisposeAsync());
        client.Logger.LogInformation(CustomLogEvents.DeleteLog, "{User}'s message was deleted in {Channel}", args.Message.Author.Username, args.Channel.Name);
    }
}