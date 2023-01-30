using DSharpPlus.Exceptions;
using System.Text.RegularExpressions;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LiveBot.Automation
{
    public partial class AutoMod
    {
        
        private readonly IWarningService _warningService;
        private readonly ILeaderboardService _leaderboardService;
        private readonly LiveBotDbContext _databaseContext;

        public AutoMod(IWarningService warningService,ILeaderboardService leaderboardService, LiveBotDbContext databaseContext)
        {
            _warningService = warningService;
            _leaderboardService = leaderboardService;
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
            ServerSettings guildSettings = _databaseContext.ServerSettings.FirstOrDefault(x => x.GuildId == e.Guild.Id);

            if (guildSettings == null || guildSettings.DeleteLogChannelId == 0) return;
            bool hasAttachment = e.Message.Attachments.Count > 0;
            DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.GuildId).Value;
            DiscordChannel deleteLogChannel = guild.GetChannel(guildSettings.DeleteLogChannelId);
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
                    embed.AddField("Message Content", convertedDeleteMessage, false);
                    embed.AddField("Had attachment?", hasAttachment ? $"{e.Message.Attachments.Count} Attachments" : "no", false);
                    await deleteLogChannel.SendMessageAsync(embed: embed);
                }
                else
                {
                    var location = $"{System.IO.Path.GetTempPath()}{e.Message.Id}-DeleteLog.txt";
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
            ServerSettings guildSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == e.Guild.Id);
            if (guildSettings == null || guildSettings.DeleteLogChannelId == 0) return;
            DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.GuildId).Value;
            DiscordChannel deleteLog = guild.GetChannel(guildSettings.DeleteLogChannelId);
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
            ServerSettings guildSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == e.Guild.Id);
            
            DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.GuildId).Value;
            if (guildSettings == null || guildSettings.UserTrafficChannelId == 0) return;
            DiscordChannel userTraffic = guild.GetChannel(guildSettings.UserTrafficChannelId);
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
            await userTraffic.SendMessageAsync(embed: embed);
        }

        public async Task User_Leave_Log(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            ServerSettings guildSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == e.Guild.Id);
            if (guildSettings == null || guildSettings.UserTrafficChannelId == 0) return;
            DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.GuildId).Value;
            DiscordChannel userTraffic = guild.GetChannel(guildSettings.UserTrafficChannelId);
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
            await userTraffic.SendMessageAsync(embed: embed);
        }

        public async Task User_Kicked_Log(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            DateTimeOffset time = DateTimeOffset.UtcNow;
            DateTimeOffset beforeTime = time.AddSeconds(-5);
            DateTimeOffset afterTime = time.AddSeconds(10);
            ServerSettings guildSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == e.Guild.Id);
            if (guildSettings == null || guildSettings.ModerationLogChannelId == 0) return;
            DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Value.Id == guildSettings.GuildId).Value;
            var logs = await guild.GetAuditLogsAsync(5, action_type: AuditLogActionType.Kick);
            DiscordChannel wkbLog = guild.GetChannel(guildSettings.ModerationLogChannelId);
            if (logs.Count == 0) return;
            if (logs[0].CreationTimestamp >= beforeTime && logs[0].CreationTimestamp <= afterTime)
            {
                await CustomMethod.SendModLogAsync(wkbLog, e.Member, $"*by {logs[0].UserResponsible.Mention}*\n**Reason:** {logs[0].Reason}", CustomMethod.ModLogType.Kick);

                ServerRanks userSettings = await _databaseContext.ServerRanks.FirstOrDefaultAsync(f => e.Member.Id == f.UserDiscordId);
                EntityEntry<ServerRanks> newEntry;
                if (userSettings is null)
                {
                    newEntry= await _databaseContext.ServerRanks.AddAsync(new ServerRanks(_databaseContext, e.Member.Id, e.Guild.Id));
                    newEntry.Entity.KickCount++;
                }
                else
                {
                    userSettings.KickCount++;
                    _databaseContext.ServerRanks.Update(userSettings);
                }
                await _databaseContext.Warnings.AddAsync(new Warnings(_databaseContext, logs[0].UserResponsible.Id, e.Member.Id, e.Guild.Id, logs[0].Reason, false, "kick"));
                await _databaseContext.SaveChangesAsync();
            }
        }

        public Task User_Banned_Log(DiscordClient client, GuildBanAddEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                var wkbSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(w => w.GuildId == e.Guild.Id);
                DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Key == wkbSettings.GuildId).Value;
                if (wkbSettings.ModerationLogChannelId != 0)
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
                    DiscordChannel wkbLog = guild.GetChannel(wkbSettings.ModerationLogChannelId);
                    if (banEntry != null)
                    {
                        Console.WriteLine("Ban reason search succeeded");
                        await CustomMethod.SendModLogAsync(wkbLog, banEntry.Target, $"**User Banned:**\t{banEntry.Target.Mention}\n*by {banEntry.UserResponsible.Mention}*\n**Reason:** {banEntry.Reason}", CustomMethod.ModLogType.Ban);
                        await _databaseContext.Warnings.AddAsync(
                            new Warnings(_databaseContext, banEntry.UserResponsible.Id, banEntry.Target.Id, e.Guild.Id, banEntry.Reason ?? "No reason specified",false, "ban")
                            );
                    }
                    else
                    {
                        Console.WriteLine("Ban Reason search failed");
                        await wkbLog.SendMessageAsync("A user got banned but failed to find data, please log manually");
                    }
                }
                var userSettings = await _databaseContext.ServerRanks.FirstOrDefaultAsync(f => e.Member.Id == f.UserDiscordId && e.Guild.Id == f.GuildId);
                if (userSettings == null)
                {
                    DiscordUser user = await client.GetUserAsync(e.Member.Id);
                    var addedEntry = await _databaseContext.ServerRanks.AddAsync(new ServerRanks(_databaseContext, user.Id, e.Guild.Id));
                    addedEntry.Entity.BanCount += 1;
                }
                else
                {
                    userSettings.BanCount += 1;
                    _databaseContext.ServerRanks.Update(userSettings);
                }
                await _databaseContext.SaveChangesAsync();
            });
            return Task.CompletedTask;
        }

        public Task User_Unbanned_Log(DiscordClient client, GuildBanRemoveEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                var wkbSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == e.Guild.Id);
                DiscordGuild guild = await client.GetGuildAsync(wkbSettings.GuildId);
                if (wkbSettings.ModerationLogChannelId != 0)
                {
                    await Task.Delay(1000);
                    var logs = await guild.GetAuditLogsAsync(1, action_type: AuditLogActionType.Unban);
                    DiscordChannel wkbLog = guild.GetChannel(wkbSettings.ModerationLogChannelId);
                    await CustomMethod.SendModLogAsync(wkbLog, e.Member, $"**User Unbanned:**\t{e.Member.Mention}\n*by {logs[0].UserResponsible.Mention}*", CustomMethod.ModLogType.Unban);
                }
            });
            return Task.CompletedTask;
        }

        public async Task Spam_Protection(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild == null) return;
            ServerSettings serverSettings = _databaseContext.ServerSettings.FirstOrDefault(w => w.GuildId == e.Guild.Id);

            if (serverSettings == null || serverSettings.ModerationLogChannelId == 0 || serverSettings.SpamExceptionChannels.Any(id => id == e.Channel.Id)) return;
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

            int infractionLevel = _databaseContext.Warnings.Count(w => w.UserDiscordId == member.Id && w.GuildId == e.Guild.Id && w.Type == "warning" && w.IsActive);

            if (infractionLevel < 5)
            {
                _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, "Spam protection triggered - flood", true));
            }
        }

        public async Task Link_Spam_Protection(DiscordClient client, MessageCreateEventArgs e)
        {
            ServerSettings serverSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == e.Guild.Id);
            if (e.Author.IsBot || serverSettings == null || serverSettings.ModerationLogChannelId == 0 || !serverSettings.HasLinkProtection) return;
            var invites = await e.Guild.GetInvitesAsync();
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (!CustomMethod.CheckIfMemberAdmin(member) && (e.Message.Content.Contains("discordapp.com/invite/") || e.Message.Content.Contains("discord.gg/")) && !invites.Any(w => e.Message.Content.Contains($"/{w.Code}")))
            {
                await e.Message.DeleteAsync();
                await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
                _warningService.AddToQueue(new WarningItem(e.Author, client.CurrentUser, e.Guild, e.Channel, $"Spam protection triggered - invite links", true));
            }
        }

        public async Task Everyone_Tag_Protection(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild == null) return;

            ServerSettings serverSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(w=>w.GuildId==e.Guild.Id);
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (
                    serverSettings != null &&
                    serverSettings.ModerationLogChannelId != 0 &&
                    serverSettings.HasEveryoneProtection &&
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
            DB.ServerSettings serverSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(w => w.GuildId == e.Guild.Id);

            if (serverSettings.VoiceActivityLogChannelId == 0) return;
            DiscordChannel vcActivityLogChannel = e.Guild.GetChannel(serverSettings.VoiceActivityLogChannelId);
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
            ServerSettings serverSettings = await _databaseContext.ServerSettings.FirstAsync(w => w.GuildId == e.Guild.Id);
            if (serverSettings.ModerationLogChannelId == 0) return;

            if (e.CommunicationDisabledUntilBefore == e.CommunicationDisabledUntilAfter) return;
            DiscordChannel userTimedOutLogChannel = e.Guild.GetChannel(serverSettings.ModerationLogChannelId);

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

        public static void ClearMSGCache()
        {
            if (_messageList.Count > 100)
            {
                _messageList.RemoveRange(0, _messageList.Count - 100);
            }
        }

        [GeneratedRegex("`[a-zA-Z0-1.,:/ ]{0,}@everyone[a-zA-Z0-1.,:/ ]{0,}`")]
        private static partial Regex EveryoneTagRegex();
    }
}