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
}

public class ModMailService : IModMailService
{
    private readonly LiveBotDbContext _dbContext;
    public int TimeoutMinutes { get; }= 120;

    public ModMailService(LiveBotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ProcessModMailDm(DiscordClient client, MessageCreateEventArgs e)
    {
        ModMail mmEntry = await _dbContext.ModMail.FirstOrDefaultAsync(w => w.UserDiscordId == e.Author.Id && w.IsActive);
        if (e.Guild == null && mmEntry != null)
        {
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
                foreach (var attachment in e.Message.Attachments)
                {
                    embed.AddField("Attachment", attachment.Url, false);
                }
            }

            mmEntry.HasChatted = true;
            mmEntry.LastMessageTime = DateTime.UtcNow;

            _dbContext.ModMail.Update(mmEntry);
            await _dbContext.SaveChangesAsync();


            ulong? modMailChannelId = _dbContext.Guilds.First(w => w.Id == mmEntry.GuildId).ModMailChannelId;
            if (modMailChannelId != null)
            {
                DiscordChannel modMailChannel = guild.GetChannel(modMailChannelId.Value);
                await modMailChannel.SendMessageAsync(embed: embed);

                client.Logger.LogInformation(CustomLogEvents.ModMail, "New Mod Mail message sent to {ChannelName}({ChannelId}) in {GuildName} from {Username}({UserId})", modMailChannel.Name, modMailChannel.Id, modMailChannel.Guild.Name, e.Author.Username, e.Author.Id);
            }
        }
    }

    public async Task CloseModMailAsync(DiscordClient client,ModMail modMail, DiscordUser closer, string closingText, string closingTextToUser)
    {
        modMail.IsActive = false;
        string notificationMessage = string.Empty;
        DiscordGuild guild = await client.GetGuildAsync(modMail.GuildId);
        DiscordChannel modMailChannel = guild.GetChannel(_dbContext.Guilds.First(w => w.Id == guild.Id).ModMailChannelId.Value);
        DiscordEmbedBuilder embed = new()
        {
            Title = $"[CLOSED] #{modMail.Id} {closingText}",
            Color = new DiscordColor(modMail.ColorHex),
            Author = new DiscordEmbedBuilder.EmbedAuthor
            {
                Name = $"{closer.Username} ({closer.Id})",
                IconUrl = closer.AvatarUrl
            },
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

        _dbContext.ModMail.Update(modMail);
        await _dbContext.SaveChangesAsync();
        await modMailChannel.SendMessageAsync(notificationMessage, embed: embed);
    }

    public async Task CloseButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Interaction.Type != InteractionType.Component || e.Interaction.User.IsBot || !e.Interaction.Data.CustomId.Contains("close")) return;
        ModMail mmEntry = await _dbContext.ModMail.FirstOrDefaultAsync(w => w.IsActive && $"{w.Id}" == e.Interaction.Data.CustomId.Replace("close", ""));
        DiscordInteractionResponseBuilder discordInteractionResponseBuilder = new();
        if (e.Message.Embeds.Count>0)
        {
            discordInteractionResponseBuilder.AddEmbeds(e.Message.Embeds);
        }
        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, discordInteractionResponseBuilder.WithContent(e.Message.Content));
        if (mmEntry == null) return;
        await CloseModMailAsync(
            client,
            mmEntry,
            e.Interaction.User,
            $" Mod Mail closed by {e.Interaction.User.Username}",
            $"**Mod Mail closed by {e.Interaction.User.Username}!\n----------------------------------------------------**");
    }

    public async Task OpenButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Interaction.Type != InteractionType.Component || e.Interaction.User.IsBot || !e.Interaction.Data.CustomId.Contains("openmodmail")) return;
            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            DiscordGuild guild = await client.GetGuildAsync(Convert.ToUInt64(e.Interaction.Data.CustomId.Replace("openmodmail","")));
            if (_dbContext.GuildUsers.First(w=>w.GuildId == guild.Id && w.UserDiscordId == e.User.Id).IsModMailBlocked)
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("You are blocked from using the Mod Mail feature in this server."));
                return;
            }
            if (_dbContext.ModMail.Any(w => w.UserDiscordId == e.User.Id && w.IsActive))
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("You already have an existing Mod Mail open, please close it before starting a new one."));
                return;
            }

            Random r = new();
            string colorId = string.Format("#{0:X6}", r.Next(0x1000000));
            ModMail newEntry = new(_dbContext,guild.Id,e.User.Id,DateTime.UtcNow, colorId)
            {
                IsActive = true,
                HasChatted = false
            };
            
            await _dbContext.ModMail.AddAsync(newEntry);
            await _dbContext.SaveChangesAsync();
            
            DiscordButtonComponent closeButton = new(ButtonStyle.Danger, $"close{newEntry.Id}", "Close", false, new DiscordComponentEmoji("✖️"));

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

            ulong? modMailChannelId = _dbContext.Guilds.First(w=>w.Id== guild.Id).ModMailChannelId;
            if (modMailChannelId != null)
            {
                DiscordChannel modMailChannel = guild.GetChannel(modMailChannelId.Value);
                await new DiscordMessageBuilder()
                    .AddComponents(closeButton)
                    .WithEmbed(embed)
                    .SendAsync(modMailChannel);
            }
    }
}