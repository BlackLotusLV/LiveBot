using System.Net.Http;
using DSharpPlus.Exceptions;
using System.Text.RegularExpressions;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

public partial class AutoMod
{

    private readonly IWarningService _warningService;
    private readonly IModLogService _modLogService;
    private readonly IDbContextFactory<LiveBotDbContext> _dbContextFactory;
    private readonly IDatabaseMethodService _databaseMethodService;
    private readonly HttpClient _httpClient;

    public AutoMod(IWarningService warningService, IModLogService modLogService, IDbContextFactory<LiveBotDbContext> dbContextFactory, IDatabaseMethodService databaseMethodService, HttpClient httpClient)
    {
        _warningService = warningService;
        _modLogService = modLogService;
        _dbContextFactory = dbContextFactory;
        _databaseMethodService = databaseMethodService;
        _httpClient = httpClient;
    }

    public async Task Bulk_Delete_Log(DiscordClient client, MessageBulkDeleteEventArgs e)
    {
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guildSettings = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id) ?? await _databaseMethodService.AddGuildAsync(new Guild(e.Guild.Id));
        if (guildSettings.DeleteLogChannelId == null) return;
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.Id).Value;
        DiscordChannel deleteLog = guild.GetChannel(guildSettings.DeleteLogChannelId.Value);
        StringBuilder sb = new();
        foreach (DiscordMessage message in e.Messages.Reverse())
        {
            if (message.Author != null)
            {
                if (!message.Author.IsBot)
                {
                    sb.AppendLine($"{message.Author.Username}{message.Author.Mention} {message.Timestamp} " +
                                  $"\n{message.Channel.Mention} - {message.Content}");
                }
            }
            else
            {
                sb.AppendLine($"Author Unknown {message.Timestamp}" +
                              $"\n- Bot was offline when this message was created.");
            }
        }

        if (sb.ToString().Length < 2000)
        {
            DiscordEmbedBuilder embed = new()
            {
                Color = new DiscordColor(0xFF6600),
                Title = "Bulk delete log",
                Description = sb.ToString()
            };
            await deleteLog.SendMessageAsync(embed: embed);
            client.Logger.LogInformation(CustomLogEvents.DeleteLog, "Bulk delete log sent");
        }
        else
        {
            await File.WriteAllTextAsync($"{Path.GetTempPath()}{e.Messages.Count}-BulkDeleteLog.txt", sb.ToString());
            await using var upFile = new FileStream($"{Path.GetTempPath()}{e.Messages.Count}-BulkDeleteLog.txt", FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            var msgBuilder = new DiscordMessageBuilder
            {
                Content = $"Bulk delete log(Over the message cap) ({e.Messages.Count}) [{e.Messages[0].Timestamp} - {e.Messages[^1].Timestamp}]"
            };
            msgBuilder.AddFile(upFile);
            await deleteLog.SendMessageAsync(msgBuilder);
            client.Logger.LogInformation(CustomLogEvents.DeleteLog, "Bulk delete log sent - over message cap");
        }
    }

    public async Task User_Join_Log(DiscordClient client, GuildMemberAddEventArgs e)
    {
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guildSettings = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id) ?? await _databaseMethodService.AddGuildAsync(new Guild(e.Guild.Id));
        if (guildSettings.UserTrafficChannelId == null)return;
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.Id).Value;
        DiscordChannel userTraffic = guild.GetChannel(guildSettings.UserTrafficChannelId.Value);
        DiscordEmbedBuilder embed = new()
        {
            Title = $"📥{e.Member.Username}({e.Member.Id}) has joined the server",
            Footer = new DiscordEmbedBuilder.EmbedFooter
            {
                IconUrl = e.Member.AvatarUrl,
                Text = $"User joined ({e.Guild.MemberCount})"
            },
            Color = new DiscordColor(0x00ff00)
        };
        embed.AddField("User tag", e.Member.Mention);

        var infractions = await liveBotDbContext.Infractions.Where(infraction => infraction.GuildId == e.Guild.Id && infraction.UserId == e.Member.Id).ToListAsync();

        embed.AddField("Infraction count", infractions.Count.ToString());
        DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder()
            .AddEmbed(embed)
            .AddComponents(
                new DiscordButtonComponent(ButtonStyle.Primary, $"{_warningService.InfractionButtonPrefix}{e.Member.Id}", "Get infractions"),
                new DiscordButtonComponent(ButtonStyle.Primary, $"{_warningService.UserInfoButtonPrefix}{e.Member.Id}", "Get User Info")
            );
        await userTraffic.SendMessageAsync(messageBuilder);
    }

    public async Task User_Leave_Log(DiscordClient client, GuildMemberRemoveEventArgs e)
    {
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guildSettings = await liveBotDbContext.Guilds.FirstOrDefaultAsync(x => x.Id == e.Guild.Id);
        if (guildSettings?.UserTrafficChannelId == null)return;
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.Id).Value;
        DiscordChannel userTraffic = guild.GetChannel(guildSettings.UserTrafficChannelId.Value);
        DiscordEmbedBuilder embed = new()
        {
            Title = $"📤{e.Member.Username}({e.Member.Id}) has left the server",
            Footer = new DiscordEmbedBuilder.EmbedFooter
            {
                IconUrl = e.Member.AvatarUrl,
                Text = $"User left ({e.Guild.MemberCount})"
            },
            Color = new DiscordColor(0xff0000)
        };
        embed.AddField("User tag", e.Member.Mention);

        var infractions = await liveBotDbContext.Infractions.Where(infraction => infraction.GuildId == e.Guild.Id && infraction.UserId == e.Member.Id).ToListAsync();

        embed.AddField("Infraction count", infractions.Count.ToString());
        DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder()
            .AddEmbed(embed)
            .AddComponents(
                new DiscordButtonComponent(ButtonStyle.Primary, $"{_warningService.InfractionButtonPrefix}{e.Member.Id}", "Get infractions"),
                new DiscordButtonComponent(ButtonStyle.Primary, $"{_warningService.UserInfoButtonPrefix}{e.Member.Id}", "Get User Info")
            );
        await userTraffic.SendMessageAsync(messageBuilder);
    }

    public async Task Link_Spam_Protection(DiscordClient client, MessageCreateEventArgs e)
    {
        if (e.Guild is null) return;
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id);
        if (e.Author.IsBot || guild?.ModerationLogChannelId == null || !guild.HasLinkProtection) return;
        var invites = await e.Guild.GetInvitesAsync();
        DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
        if (!CustomMethod.CheckIfMemberAdmin(member)
            && !e.Message.Content.Contains("?event=")
            && (e.Message.Content.Contains("discordapp.com/invite/")
                || e.Message.Content.Contains("discord.gg/"))
            && !invites.Any(x => e.Message.Content.Contains($"/{x.Code}"))
            && !e.Message.Content.Contains($"/{e.Guild.VanityUrlCode}"))
        {
            await e.Message.DeleteAsync();
            await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1), "Spam protection triggered - invite links");
            _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, "Spam protection triggered - invite links", true));
            client.Logger.LogInformation("User {Username}({UserId}) tried to post an invite link in {GuildName}({GuildId})",
                e.Author.Username, e.Author.Id, e.Guild.Name, e.Guild.Id);
        }
    }

    public async Task Everyone_Tag_Protection(DiscordClient client, MessageCreateEventArgs e)
    {
        if (e.Author.IsBot || e.Guild is null) return;

        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id);
        DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
        if (
            guild is { ModerationLogChannelId: not null, HasEveryoneProtection: true } &&
            !member.Permissions.HasPermission(Permissions.MentionEveryone) &&
            e.Message.Content.Contains("@everyone") &&
            !EveryoneTagRegex().IsMatch(e.Message.Content)
        )
        {
            var msgDeleted = false;
            try
            {
                await e.Message.DeleteAsync();
            }
            catch (NotFoundException)
            {
                msgDeleted = true;
            }

            if (!msgDeleted)
            {
                await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1), "Spam protection triggered - everyone tag");
                _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, "Tried to tag everyone", true));
            }
        }
    }

    public async Task Voice_Activity_Log(DiscordClient client, VoiceStateUpdateEventArgs e)
    {
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id) ?? await _databaseMethodService.AddGuildAsync(new Guild(e.Guild.Id));

        if (guild.VoiceActivityLogChannelId == null) return;
        DiscordChannel vcActivityLogChannel = e.Guild.GetChannel(guild.VoiceActivityLogChannelId.Value);
        DiscordEmbedBuilder embed = new()
        {
            Author = new DiscordEmbedBuilder.EmbedAuthor
            {
                IconUrl = e.User.AvatarUrl,
                Name = $"{e.User.Username} ({e.User.Id})"
            },
            Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
            {
                Url = e.User.AvatarUrl
            }
        };
        if (e?.After?.Channel != null && e?.Before?.Channel == null)
        {
            embed.Title = "➡ [JOINED] ➡";
            embed.Color = DiscordColor.Green;
            embed.AddField("Channel joined", $"**{e.After.Channel.Name}** *({e.After.Channel.Id})*", false);
        }
        else if (e?.After?.Channel == null && e?.Before?.Channel != null)
        {
            embed.Title = "⬅ [LEFT] ⬅";
            embed.Color = DiscordColor.Red;
            embed.AddField("Channel left", $"**{e.Before.Channel.Name}** *({e.Before.Channel.Id})*", false);
        }
        else if (e?.After?.Channel != null && e?.Before?.Channel != null && e?.After?.Channel != e?.Before?.Channel)
        {
            embed.Title = "🔄 [SWITCHED] 🔄";
            embed.Color = new DiscordColor(0x87CEFF);
            embed.AddField("Channel left", $"**{e.Before.Channel.Name}** *({e.Before.Channel.Id})*", false);
            embed.AddField("Channel joined", $"**{e.After.Channel.Name}** *({e.After.Channel.Id})*", false);
        }

        if (e?.After?.Channel != e?.Before?.Channel)
        {
            await vcActivityLogChannel.SendMessageAsync(embed);
        }
    }

    [GeneratedRegex("`[a-zA-Z0-1.,:/ ]{0,}@everyone[a-zA-Z0-1.,:/ ]{0,}`")]
    private static partial Regex EveryoneTagRegex();
}