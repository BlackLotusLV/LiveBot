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
        private readonly LiveBotDbContext _databaseContext;

        public AutoMod(IWarningService warningService, LiveBotDbContext databaseContext)
        {
            _warningService = warningService;
            _databaseContext = databaseContext;
        }
        
        private static readonly ulong[] MediaOnlyChannelIDs = new ulong[] { 191567033064751104, 447134224349134848, 404613175024025601, 195095947871518721, 469920292374970369 };

        private static List<DiscordMessage> _messageList = new();

        public Task Media_Only_Filter(DiscordClient client, MessageCreateEventArgs e)
        {
            _ = Task.Run(async () =>
                {
                    if (MediaOnlyChannelIDs.Any(id => id == e.Channel.Id) && !e.Author.IsBot && e.Message.Attachments.Count == 0 && !e.Message.Content.Split(' ').Any(a => Uri.TryCreate(a, UriKind.Absolute, out _)))
                    {
                        await e.Message.DeleteAsync();
                        DiscordMessage m = await e.Channel.SendMessageAsync("This channel is for sharing media only, please use the content comment channel for discussions. If this is a mistake please contact a moderator.");
                        await Task.Delay(9000);
                        await m.DeleteAsync();
                        client.Logger.LogInformation(CustomLogEvents.PhotoCleanup, "User tried to send text in photomode channel. Message deleted");
                    }
                });
            return Task.CompletedTask;
        }

        public async Task Delete_Log(DiscordClient client, MessageDeleteEventArgs e)
        {
            if (e.Guild == null) return;
            DiscordMessage msg = e.Message;
            DiscordUser author = msg.Author;
            Guild guildSettings = _databaseContext.Guilds.FirstOrDefault(x => x.Id == e.Guild.Id);

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
                    await deleteLogChannel.SendMessageAsync(embed: embed);
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
            DiscordMessage deletedMsg = _messageList.FirstOrDefault(w => w.Timestamp.Equals(e.Message.Timestamp) && w.Content.Equals(e.Message.Content));
            if (deletedMsg != null)
            {
                _messageList.Remove(deletedMsg);
            }
        }

        public async Task Bulk_Delete_Log(DiscordClient client, MessageBulkDeleteEventArgs e)
        {
            Guild guildSettings = await _databaseContext.Guilds.FirstOrDefaultAsync(x => x.Id == e.Guild.Id);
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
            Guild guildSettings = await _databaseContext.Guilds.FindAsync(e.Guild.Id) ?? await _databaseContext.AddGuildAsync(_databaseContext, new Guild(e.Guild.Id));
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

            var infractions = await _databaseContext.Infractions.Where(infraction => infraction.GuildId == e.Guild.Id && infraction.UserId == e.Member.Id).ToListAsync();
            
            embed.AddField("Infraction count", infractions.Count.ToString());
            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, $"GetInfractions-{e.Member.Id}", "Get infractions"));
            await userTraffic.SendMessageAsync(messageBuilder);
        }

        public async Task User_Leave_Log(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            Guild guildSettings = await _databaseContext.Guilds.FirstOrDefaultAsync(x => x.Id == e.Guild.Id);
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

            var infractions = await _databaseContext.Infractions.Where(infraction => infraction.GuildId == e.Guild.Id && infraction.UserId == e.Member.Id).ToListAsync();
            
            embed.AddField("Infraction count", infractions.Count.ToString());
            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, $"GetInfractions-{e.Member.Id}", "Get infractions"));
            await userTraffic.SendMessageAsync(messageBuilder);
        }

        public async Task User_Banned_Log(DiscordClient client, GuildBanAddEventArgs e)
        {
                var wkbSettings = await _databaseContext.Guilds.FirstOrDefaultAsync(w => w.Id == e.Guild.Id);
                DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Key == wkbSettings.Id).Value;
                if (wkbSettings.ModerationLogChannelId != null)
                {
                    int timesRun = 0;
                    Console.WriteLine("--Ban triggered--");
                    IReadOnlyList<DiscordAuditLogEntry> entries = await e.Guild.GetAuditLogsAsync(20, null, AuditLogActionType.Ban);
                    DiscordAuditLogBanEntry banEntry = entries.Select(entry => entry as DiscordAuditLogBanEntry).FirstOrDefault(entry => entry.Target == e.Member);
                    while (banEntry == null && timesRun < 15)
                    {
                        await Task.Delay(2000);
                        entries = await e.Guild.GetAuditLogsAsync(5, null, AuditLogActionType.Ban);
                        banEntry = entries.Select(entry => entry as DiscordAuditLogBanEntry).FirstOrDefault(entry => entry.Target == e.Member);
                        timesRun++;
                        Console.WriteLine($"--Trying check again {timesRun}. {(banEntry == null ? "Empty" : "Found")}");
                    }
                    DiscordChannel wkbLog = guild.GetChannel(wkbSettings.ModerationLogChannelId.Value);
                    if (banEntry != null)
                    {
                        Console.WriteLine("Ban reason search succeeded");
                        await CustomMethod.SendModLogAsync(wkbLog, banEntry.Target,
                            $"**User Banned:**\t{banEntry.Target.Mention}\n*by {banEntry.UserResponsible.Mention}*\n**Reason:** {banEntry.Reason}", CustomMethod.ModLogType.Ban);
                        await _databaseContext.AddInfractionsAsync(_databaseContext,
                            new Infraction(banEntry.UserResponsible.Id, banEntry.Target.Id, e.Guild.Id, banEntry.Reason ?? "No reason specified", false, InfractionType.Ban));
                    }
                    else
                    {
                        Console.WriteLine("Ban Reason search failed");
                        await wkbLog.SendMessageAsync("A user got banned but failed to find data, please log manually");
                    }
                }

                GuildUser guildUser = await _databaseContext.GuildUsers.FindAsync(new object[] { e.Member.Id, e.Guild.Id }) ??
                                      await _databaseContext.AddGuildUsersAsync(_databaseContext, new GuildUser(e.Member.Id, e.Guild.Id));
                guildUser.BanCount++;
                _databaseContext.Update(guildUser);
                await _databaseContext.SaveChangesAsync();
        }

        public Task User_Unbanned_Log(DiscordClient client, GuildBanRemoveEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                Guild wkbSettings = await _databaseContext.Guilds.FindAsync(e.Guild.Id);
                DiscordGuild guild = await client.GetGuildAsync(wkbSettings.Id);
                if (wkbSettings.ModerationLogChannelId != null)
                {
                    await Task.Delay(1000);
                    var logs = await guild.GetAuditLogsAsync(1, action_type: AuditLogActionType.Unban);
                    DiscordChannel wkbLog = guild.GetChannel(wkbSettings.ModerationLogChannelId.Value);
                    await CustomMethod.SendModLogAsync(wkbLog, e.Member, $"**User Unbanned:**\t{e.Member.Mention}\n*by {logs[0].UserResponsible.Mention}*", CustomMethod.ModLogType.Unban);
                }
            });
            return Task.CompletedTask;
        }

        public async Task Spam_Protection(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild == null) return;
            Guild guild = _databaseContext.Guilds.Include(g=>g.SpamIgnoreChannels).FirstOrDefault(w => w.Id == e.Guild.Id);

            if (guild?.ModerationLogChannelId == null || guild.SpamIgnoreChannels.Any(x=>x.ChannelId==e.Channel.Id)) return;
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);

            if (CustomMethod.CheckIfMemberAdmin(member)) return;
            _messageList.Add(e.Message);
            List<DiscordMessage> duplicateMessages = _messageList.Where(w => w.Author == e.Author && w.Content == e.Message.Content && e.Guild == w.Channel.Guild).ToList();
            int i = duplicateMessages.Count;
            if (i < 5) return;

            TimeSpan time = (duplicateMessages[i - 1].CreationTimestamp - duplicateMessages[i - 5].CreationTimestamp) / 5;
            if (time >= TimeSpan.FromSeconds(6)) return;
            
            List<DiscordChannel> channelList = duplicateMessages.GetRange(i - 5, 5).Select(s => s.Channel).Distinct().ToList();
            await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
            foreach (DiscordChannel channel in channelList)
            {
                await channel.DeleteMessagesAsync(duplicateMessages.GetRange(i - 5, 5));
            }

            int infractionLevel = _databaseContext.Infractions.Count(w => w.UserId == member.Id && w.GuildId == e.Guild.Id && w.InfractionType == InfractionType.Warning && w.IsActive);

            if (infractionLevel < 5)
            {
                _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, "Spam protection triggered - flood", true));
            }
        }

        public async Task Link_Spam_Protection(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Guild == null) return;
            Guild guild = await _databaseContext.Guilds.FindAsync(e.Guild.Id);
            if (e.Author.IsBot || guild == null || guild.ModerationLogChannelId == null || !guild.HasLinkProtection) return;
            var invites = await e.Guild.GetInvitesAsync();
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (!CustomMethod.CheckIfMemberAdmin(member) && !e.Message.Content.Contains("?event=") && (e.Message.Content.Contains("discordapp.com/invite/") || e.Message.Content.Contains("discord.gg/")) && !invites.Any(w => e.Message.Content.Contains($"/{w.Code}")))
            {
                await e.Message.DeleteAsync();
                await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
                _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, $"Spam protection triggered - invite links", true));
            }
        }

        public async Task Everyone_Tag_Protection(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild == null) return;

            Guild guild = await _databaseContext.Guilds.FirstOrDefaultAsync(w=>w.Id==e.Guild.Id);
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (
                    guild != null &&
                    guild.ModerationLogChannelId != null &&
                    guild.HasEveryoneProtection &&
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
                    await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
                    _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, $"Tried to tag everyone", true));
                }
            }
        }

        public async Task Voice_Activity_Log(DiscordClient client, VoiceStateUpdateEventArgs e)
        {
            DB.Guild guild = await _databaseContext.Guilds.FirstOrDefaultAsync(w => w.Id == e.Guild.Id);

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

        public async Task User_Timed_Out_Log(DiscordClient client, GuildMemberUpdateEventArgs e)
        {
            if (e.Member.IsBot) return;
            Guild guild = await _databaseContext.Guilds.FirstAsync(w => w.Id == e.Guild.Id);
            if (guild.ModerationLogChannelId == null) return;

            if (e.CommunicationDisabledUntilBefore == e.CommunicationDisabledUntilAfter) return;
            DiscordChannel userTimedOutLogChannel = e.Guild.GetChannel(guild.ModerationLogChannelId.Value);

            DateTimeOffset dto = e.Member.CommunicationDisabledUntil.GetValueOrDefault();
            if (e.CommunicationDisabledUntilAfter != null && e.CommunicationDisabledUntilBefore == null)
            {
                await CustomMethod.SendModLogAsync(userTimedOutLogChannel, e.Member, $"**Timed Out Until:** <t:{dto.ToUnixTimeSeconds()}:F>(<t:{dto.ToUnixTimeSeconds()}:R>)", CustomMethod.ModLogType.TimedOut);
            }
            else if (e.CommunicationDisabledUntilAfter == null && e.CommunicationDisabledUntilBefore != null)
            {
                await CustomMethod.SendModLogAsync(userTimedOutLogChannel, e.Member, $"**Timeout Removed**", CustomMethod.ModLogType.TimeOutRemoved);
            }
        }

        [GeneratedRegex("`[a-zA-Z0-1.,:/ ]{0,}@everyone[a-zA-Z0-1.,:/ ]{0,}`")]
        private static partial Regex EveryoneTagRegex();
    }
}