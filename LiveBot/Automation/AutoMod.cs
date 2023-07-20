using System.Collections.Immutable;
using System.Net.Http;
using DSharpPlus.Exceptions;
using System.Text.RegularExpressions;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation
{
    public partial class AutoMod
    {
        
        private readonly IWarningService _warningService;
        private readonly IModLogService _modLogService;
        private readonly DbContextFactory _dbContextFactory;

        public AutoMod(IWarningService warningService, IModLogService modLogService, DbContextFactory dbContextFactory)
        {
            _warningService = warningService;
            _modLogService = modLogService;
            _dbContextFactory = dbContextFactory;
        }
        
        private static readonly ulong[] MediaOnlyChannelIDs = new ulong[] { 191567033064751104, 447134224349134848, 404613175024025601, 195095947871518721, 469920292374970369 };

        public async Task Media_Only_Filter(DiscordClient client, MessageCreateEventArgs e)
        {
            if (MediaOnlyChannelIDs.Any(id => id == e.Channel.Id) &&
                !e.Author.IsBot &&
                e.Message.Attachments.Count == 0 &&
                !e.Message.Content.Split(' ').Any(a => Uri.TryCreate(a, UriKind.Absolute, out _)))
            {
                await e.Message.DeleteAsync();
                DiscordMessage m = await e.Channel.SendMessageAsync(
                    "This channel is for sharing media only, please use the content comment channel for discussions. If this is a mistake please contact a moderator.");
                await Task.Delay(9000);
                await m.DeleteAsync();
                client.Logger.LogInformation(CustomLogEvents.PhotoCleanup,
                    "User tried to send text in photomode channel. Message deleted");
            }
        }

        public async Task Delete_Log(DiscordClient client, MessageDeleteEventArgs e)
        {
            if (e.Guild == null) return;
            LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            DiscordMessage msg = e.Message;
            DiscordUser author = msg.Author;
            Guild guildSettings = liveBotDbContext.Guilds.FirstOrDefault(x => x.Id == e.Guild.Id);

            if (guildSettings == null || guildSettings.DeleteLogChannelId == null) return;
            bool hasAttachment = e.Message.Attachments.Count > 0;
            DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.Id).Value;
            DiscordChannel deleteLogChannel = guild.GetChannel(guildSettings.DeleteLogChannelId.Value);
            if (author != null && !author.IsBot)
            {
                string convertedDeleteMessage = msg.Content;
                if (convertedDeleteMessage == "")
                {
                    convertedDeleteMessage = "*message didn't contain any text*";
                }

                var description = $"{author.Mention}'s message was deleted in {e.Channel.Mention}";
                if (convertedDeleteMessage.Length <= 1024)
                {
                    DiscordMessageBuilder msgBuilder = new();
                    DiscordEmbedBuilder embed = new()
                    {
                        Color = new DiscordColor(0xFF6600),
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            IconUrl = author.AvatarUrl,
                            Name = $"{author.Username}'s message deleted"
                        },
                        Description = description,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = $"Time posted: {msg.CreationTimestamp}"
                        }
                    };
                    embed.AddField("Message Content", convertedDeleteMessage);
                    embed.AddField("Had attachment?", hasAttachment ? $"{e.Message.Attachments.Count} Attachments" : "no");

                    List<DiscordEmbed> attachmentEmbeds = new();
                    List<string> imageExtensions = new() { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tiff", ".jfif", ".svg", ".ico" };
                    foreach (DiscordAttachment messageAttachment in e.Message.Attachments.Where(x=>imageExtensions.Contains(Path.GetExtension(x.FileName))))
                    {
                        using HttpClient httpClient = new();
                        HttpResponseMessage response = await httpClient.GetAsync(messageAttachment.Url);
                        if (!response.IsSuccessStatusCode) continue;
                        var memoryStream = new MemoryStream();
                        await response.Content.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;
                        var uniqueFileName = $"{Guid.NewGuid()}-{messageAttachment.FileName}";
                        msgBuilder.AddFile(uniqueFileName, memoryStream);
                        if (embed.ImageUrl is null)
                        {
                            embed.ImageUrl = $"attachment://{uniqueFileName}";
                        }
                        else
                        {
                            attachmentEmbeds.Add(new DiscordEmbedBuilder
                            {
                                Color = new DiscordColor(0xFF6600),
                                ImageUrl = $"attachment://{uniqueFileName}"
                            }.Build());
                        }
                    }
                    attachmentEmbeds.Insert(0,embed.Build());
                    msgBuilder.AddEmbeds(attachmentEmbeds);
                    
                    await deleteLogChannel.SendMessageAsync(msgBuilder);
                }
                else
                {
                    var location = $"{Path.GetTempPath()}{e.Message.Id}-DeleteLog.txt";
                    await File.WriteAllTextAsync(location, $"{description}\n**Contents:** {convertedDeleteMessage}");
                    await using var upFile = new FileStream(location, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                    var msgBuilder = new DiscordMessageBuilder
                    {
                        Content = $"Deleted message and info too long, uploading file instead."
                    };
                    msgBuilder.AddFile(upFile);

                    await deleteLogChannel.SendMessageAsync(msgBuilder);
                }
            }
        }

        public async Task Bulk_Delete_Log(DiscordClient client, MessageBulkDeleteEventArgs e)
        {
            LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            Guild guildSettings = await liveBotDbContext.Guilds.FirstOrDefaultAsync(x => x.Id == e.Guild.Id);
            if (guildSettings == null || guildSettings.DeleteLogChannelId == null) return;
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
            }
        }

        public async Task User_Join_Log(DiscordClient client, GuildMemberAddEventArgs e)
        {
            LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            Guild guildSettings = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id) ?? await liveBotDbContext.AddGuildAsync(liveBotDbContext, new Guild(e.Guild.Id));
            if (guildSettings.UserTrafficChannelId == null) return;
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
                Color = new DiscordColor(0x00ff00),
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
            LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            Guild guildSettings = await liveBotDbContext.Guilds.FirstOrDefaultAsync(x => x.Id == e.Guild.Id);
            if (guildSettings == null || guildSettings.UserTrafficChannelId == null) return;
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
                Color = new DiscordColor(0xff0000),
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
            LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id);
            if (e.Author.IsBot || guild == null || guild.ModerationLogChannelId == null || !guild.HasLinkProtection) return;
            var invites = await e.Guild.GetInvitesAsync();
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (!CustomMethod.CheckIfMemberAdmin(member) && !e.Message.Content.Contains("?event=") && (e.Message.Content.Contains("discordapp.com/invite/") || e.Message.Content.Contains("discord.gg/")) && !invites.Any(w => e.Message.Content.Contains($"/{w.Code}")))
            {
                await e.Message.DeleteAsync();
                await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1),"Spam protection triggered - invite links");
                _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, $"Spam protection triggered - invite links", true));
            }
        }

        public async Task Everyone_Tag_Protection(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild is null) return;

            LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
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
                    await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1),"Spam protection triggered - everyone tag");
                    _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, $"Tried to tag everyone", true));
                }
            }
        }

        public async Task Voice_Activity_Log(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id) ?? await liveBotDbContext.AddGuildAsync(liveBotDbContext, new Guild(e.Guild.Id));

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
}