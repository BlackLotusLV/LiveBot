using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Services;

public interface IModMailService
{
     public int TimeoutMinutes { get; }
     Task ProcessModMailDm(DiscordClient client, MessageCreateEventArgs e);
     Task CloseModMailAsync(DiscordClient client, ModMail modMail, DiscordUser closer, string closingText, string closingTextToUser);
     Task CloseButton(DiscordClient client, ComponentInteractionCreateEventArgs e);
     Task OpenButton(DiscordClient client, ComponentInteractionCreateEventArgs e);
     public string CloseButtonPrefix { get; }
     public string OpenButtonPrefix { get; }
     Task ModMailCleanupAsync(DiscordClient client);
}

public class ModMailService : IModMailService
{
    private readonly IDbContextFactory<LiveBotDbContext> _dbContextFactory;
    private readonly IDatabaseMethodService _databaseMethodService;
    private readonly ILogger<ModLogService> _logger;
    public int TimeoutMinutes => 120;

    public string CloseButtonPrefix => "closeModMail";
    public string OpenButtonPrefix => "openModMail";

    public ModMailService(IDbContextFactory<LiveBotDbContext> dbContextFactory, IDatabaseMethodService databaseMethodService, ILoggerFactory loggerFactory)
    {
        _dbContextFactory = dbContextFactory;
        _databaseMethodService = databaseMethodService;
        _logger = loggerFactory.CreateLogger<ModLogService>();
    }

    public async Task ProcessModMailDm(DiscordClient client, MessageCreateEventArgs e)
    {
        if (e.Guild is not null) return;
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        ModMail mmEntry = await liveBotDbContext.ModMail.FirstOrDefaultAsync(w => w.UserDiscordId == e.Author.Id && w.IsActive);
        if (mmEntry == null) return;
        DiscordGuild guild = client.Guilds.First(w => w.Value.Id == mmEntry.GuildId).Value;
        DiscordEmbedBuilder embed = new()
        {
            Author = new DiscordEmbedBuilder.EmbedAuthor
            {
                IconUrl = e.Author.AvatarUrl,
                Name = $"{e.Author.Username} ({e.Author.Id})"
            },
            Color = new DiscordColor(mmEntry.ColorHex),
            Title = $"[INBOX] #{mmEntry.Id} Mod Mail user message.",
            Description = e.Message.Content
        };

        if (e.Message.Attachments != null)
        {
            foreach (DiscordAttachment attachment in e.Message.Attachments)
            {
                embed.AddField("Attachment", attachment.Url);
            }
        }

        mmEntry.HasChatted = true;
        mmEntry.LastMessageTime = DateTime.UtcNow;

        liveBotDbContext.ModMail.Update(mmEntry);
        await liveBotDbContext.SaveChangesAsync();


        ulong? modMailChannelId = liveBotDbContext.Guilds.First(w => w.Id == mmEntry.GuildId).ModMailChannelId;
        if (modMailChannelId is not null)
        {
            DiscordChannel modMailChannel = guild.GetChannel(modMailChannelId.Value);
            await modMailChannel.SendMessageAsync(embed: embed);

            client.Logger.LogInformation(CustomLogEvents.ModMail, "New Mod Mail message sent to {ChannelName}({ChannelId}) in {GuildName} from {Username}({UserId})", modMailChannel.Name,
                modMailChannel.Id, modMailChannel.Guild.Name, e.Author.Username, e.Author.Id);
        }
    }

    public async Task CloseModMailAsync(DiscordClient client,ModMail modMail, DiscordUser closer, string closingText, string closingTextToUser)
    {
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        modMail.IsActive = false;
        var notificationMessage = string.Empty;
        DiscordGuild guild = await client.GetGuildAsync(modMail.GuildId);
        Guild dbGuild = await liveBotDbContext.Guilds.FindAsync(guild.Id) ?? (await liveBotDbContext.Guilds.AddAsync(new Guild(guild.Id))).Entity;
        if (dbGuild.ModMailChannelId == null)
        {
            client.Logger.LogWarning("User tried to close mod mail, mod mail channel was not found. Something is set up incorrectly. Server ID:{ServerId}",guild.Id);
            return;
        }
        DiscordChannel modMailChannel = guild.GetChannel(dbGuild.ModMailChannelId.Value);
        DiscordEmbedBuilder embed = new()
        {
            Title = $"[CLOSED] #{modMail.Id} {closingText}",
            Color = new DiscordColor(modMail.ColorHex),
            Author = new DiscordEmbedBuilder.EmbedAuthor
            {
                Name = $"{closer.Username} ({closer.Id})",
                IconUrl = closer.AvatarUrl
            }
        };
        try
        {
            DiscordMember member = await guild.GetMemberAsync(modMail.UserDiscordId);
            await member.SendMessageAsync(closingTextToUser);
        }
        catch
        {
            notificationMessage = "User could not be contacted anymore, either blocked the bot, left the server or turned off DMs";
        }

        liveBotDbContext.ModMail.Update(modMail);
        await liveBotDbContext.SaveChangesAsync();
        await modMailChannel.SendMessageAsync(notificationMessage, embed: embed);
        _logger.LogInformation(CustomLogEvents.ModMail,"Mod Mail #{ModMailId} closed by {Username}({UserId}) in {GuildName}",modMail.Id,closer.Username,closer.Id,guild.Name);
    }

    public async Task CloseButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        if (e.Interaction.Type != InteractionType.Component || e.Interaction.User.IsBot || !e.Interaction.Data.CustomId.Contains(CloseButtonPrefix))return;
        ModMail mmEntry = await liveBotDbContext.ModMail.FindAsync(Convert.ToInt64(e.Interaction.Data.CustomId.Replace(CloseButtonPrefix, "")));
        DiscordInteractionResponseBuilder discordInteractionResponseBuilder = new();
        if (e.Message.Embeds.Count>0)
        {
            discordInteractionResponseBuilder.AddEmbeds(e.Message.Embeds);
        }
        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, discordInteractionResponseBuilder.WithContent(e.Message.Content));
        if (mmEntry is not { IsActive: true }) return;
        await CloseModMailAsync(
            client,
            mmEntry,
            e.Interaction.User,
            $" Mod Mail closed by {e.Interaction.User.Username}",
            $"**Mod Mail closed by {e.Interaction.User.Username}!\n----------------------------------------------------**");
    }

    public async Task OpenButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Interaction.Type != InteractionType.Component || e.Interaction.User.IsBot || !e.Interaction.Data.CustomId.Contains(OpenButtonPrefix)) return;
            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            DiscordGuild guild = await client.GetGuildAsync(Convert.ToUInt64(e.Interaction.Data.CustomId.Replace(OpenButtonPrefix,"")));
            await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
            if (liveBotDbContext.GuildUsers.First(w=>w.GuildId == guild.Id && w.UserDiscordId == e.User.Id).IsModMailBlocked)
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("You are blocked from using the Mod Mail feature in this server."));
                return;
            }
            if (liveBotDbContext.ModMail.Any(w => w.UserDiscordId == e.User.Id && w.IsActive))
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("You already have an existing Mod Mail open, please close it before starting a new one."));
                return;
            }

            Random r = new();
            var colorId = $"#{r.Next(0x1000000):X6}";
            ModMail newEntry = new(guild.Id,e.User.Id,DateTime.UtcNow, colorId)
            {
                IsActive = true,
                HasChatted = false
            };

            await _databaseMethodService.AddModMailAsync(newEntry);
            
            DiscordButtonComponent closeButton = new(ButtonStyle.Danger, $"{CloseButtonPrefix}{newEntry.Id}", "Close", false, new DiscordComponentEmoji("✖️"));

            await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddComponents(closeButton).WithContent($"**----------------------------------------------------**\n" +
                            $"Modmail entry **open** with `{guild.Name}`. Continue to write as you would normally ;)\n*Mod Mail will time out in {TimeoutMinutes} minutes after last message is sent.*\n" +
                            $"**Subject: No subject, Mod Mail Opened with button**"));

            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{e.User.Username} ({e.User.Id})",
                    IconUrl = e.User.AvatarUrl
                },
                Title = $"[NEW] #{newEntry.Id} Mod Mail created by {e.User.Username}.",
                Color = new DiscordColor(colorId),
                Description = "No subject, Mod Mail Opened with button"
            };

            ulong? modMailChannelId = liveBotDbContext.Guilds.First(w=>w.Id== guild.Id).ModMailChannelId;
            if (modMailChannelId != null)
            {
                DiscordChannel modMailChannel = guild.GetChannel(modMailChannelId.Value);
                await new DiscordMessageBuilder()
                    .AddComponents(closeButton)
                    .WithEmbed(embed)
                    .SendAsync(modMailChannel);
            }
    }

    public async Task ModMailCleanupAsync(DiscordClient client)
    {
        _logger.LogDebug(CustomLogEvents.ModMail, "Mod Mail cleanup started");
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        foreach (ModMail modMail in liveBotDbContext.ModMail.Where(mMail=>mMail.IsActive && mMail.LastMessageTime.AddMinutes(TimeoutMinutes) < DateTime.UtcNow).ToList())
        {
            await CloseModMailAsync(client, modMail, client.CurrentUser, " Mod Mail timed out.", "**Mod Mail timed out.**\n----------------------------------------------------");
        }

        _logger.LogDebug(CustomLogEvents.ModMail, "Mod Mail cleanup finished");
    }
}