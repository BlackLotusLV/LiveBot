﻿using DSharpPlus.Exceptions;
using System.Text.RegularExpressions;
using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation
{
    public partial class AutoMod
    {
        
        private readonly IWarningService _warningService;
        private readonly ILeaderboardService _leaderboardService;

        public AutoMod(IWarningService warningService,ILeaderboardService leaderboardService)
        {
            _warningService = warningService;
            _leaderboardService = leaderboardService;
        }
        
        private static readonly ulong[] MediaOnlyChannelIDs = new ulong[] { 191567033064751104, 447134224349134848, 404613175024025601, 195095947871518721, 469920292374970369 };

#pragma warning disable IDE0044 // Add readonly modifier
        private static List<DiscordMessage> _messageList = new();
#pragma warning restore IDE0044 // Add readonly modifier

        public Task Add_To_Leaderboards(object O, GuildMemberAddEventArgs e)
        {
            DB.ServerRanks local = DB.DBLists.ServerRanks.AsParallel().FirstOrDefault(lb => lb.User_ID == e.Member.Id && lb.Server_ID == e.Guild.Id);
            if (local is null)
            {
                _leaderboardService.QueueLeaderboardItem(e.Member, e.Guild);
            }
            return Task.CompletedTask;
        }

        public async Task Banned_Words(DiscordClient Client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild == null) return;
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (CustomMethod.CheckIfMemberAdmin(member)) return;
            List<AMBannedWords> wordlist = (from bw in DB.DBLists.AMBannedWords
                            where bw.Server_ID == e.Guild.Id
                            select bw).ToList();
            foreach (AMBannedWords word in from word in wordlist
                                 where Regex.IsMatch(e.Message.Content.ToLower(), @$"\b{word.Word}\b")
                                 select word)
            {
                bool msgDeleted = false;
                try
                {
                    await e.Message.DeleteAsync();
                }
                catch (NotFoundException)
                {
                    msgDeleted = true;
                }

                if (msgDeleted || DBLists.Warnings.Count(w => w.User_ID == e.Author.Id && w.Server_ID == e.Guild.Id && w.Type == "warning" && w.Active) >= 5) return;
                if (word.Offense.Contains("ASCII"))
                {
                    _warningService.QueueWarning(e.Author, Program.Client.CurrentUser, e.Guild, e.Channel, $"{word.Offense}", true);
                    Client.Logger.LogInformation(CustomLogEvents.AutoMod, "User {Username}({UserId}) Warned for ASCII spam", e.Author.Username, e.Author.Id);
                }
                else
                {
                    _warningService.QueueWarning(e.Author, Program.Client.CurrentUser, e.Guild, e.Channel, $"{word.Offense} - Trigger word: `{word.Word}`", true);
                    Client.Logger.LogInformation(CustomLogEvents.AutoMod, "User {Username}({UserId}) Warned for using a trigger word.\n\t\tOffense - {Offense}\n\t\tContent - {TriggerWord}", e.Author.Username, e.Author.Id, word.Offense, word.Word);
                }
            }
        }

        public static Task Media_Only_Filter(DiscordClient Client, MessageCreateEventArgs e)
        {
            _ = Task.Run(async () =>
                {
                    if (MediaOnlyChannelIDs.Any(id => id == e.Channel.Id) && !e.Author.IsBot && e.Message.Attachments.Count == 0 && !e.Message.Content.Split(' ').Any(a => Uri.TryCreate(a, UriKind.Absolute, out _)))
                    {
                        await e.Message.DeleteAsync();
                        DiscordMessage m = await e.Channel.SendMessageAsync("This channel is for sharing media only, please use the content comment channel for discussions. If this is a mistake please contact a moderator.");
                        await Task.Delay(9000);
                        await m.DeleteAsync();
                        Client.Logger.LogInformation(CustomLogEvents.PhotoCleanup, "User tried to send text in photomdoe channel. Message deleted");
                    }
                });
            return Task.CompletedTask;
        }

        public static async Task Delete_Log(DiscordClient Client, MessageDeleteEventArgs e)
        {
            if (e.Guild == null) return;
            DiscordMessage msg = e.Message;
            DiscordUser author = msg.Author;
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id
                                 select ss).FirstOrDefault();
            string Description = string.Empty;

            if (GuildSettings == null || GuildSettings.Delete_Log == 0) return;
            bool HasAttachment = e.Message.Attachments.Count > 0;
            DiscordGuild Guild = Client.Guilds.FirstOrDefault(w => w.Value.Id == GuildSettings.ID_Server).Value;
            DiscordChannel DeleteLog = Guild.GetChannel(GuildSettings.Delete_Log);
            if (author != null && !author.IsBot)
            {
                string converteddeletedmsg = msg.Content;
                if (converteddeletedmsg == "")
                {
                    converteddeletedmsg = "*message didn't contain any text*";
                }

                Description = $"{author.Mention}'s message was deleted in {e.Channel.Mention}";
                if (converteddeletedmsg.Length <= 1024)
                {
                    DiscordEmbedBuilder embed = new()
                    {
                        Color = new DiscordColor(0xFF6600),
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            IconUrl = author.AvatarUrl,
                            Name = $"{author.Username}'s message deleted"
                        },
                        Description = Description,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = $"Time posted: {msg.CreationTimestamp}"
                        }
                    };
                    embed.AddField("Message Content", converteddeletedmsg, false);
                    embed.AddField("Had attachment?", HasAttachment ? $"{e.Message.Attachments.Count} Attachments" : "no", false);
                    await DeleteLog.SendMessageAsync(embed: embed);
                }
                else
                {
                    string location = $"{Program.TmpLoc}{e.Message.Id}-DeleteLog.txt";
                    File.WriteAllText(location, $"{Description}\n**Contents:** {converteddeletedmsg}");
                    using var upFile = new FileStream(location, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                    var msgBuilder = new DiscordMessageBuilder
                    {
                        Content = $"Deleted message and info too long, uploading file instead."
                    };
                    msgBuilder.AddFile(upFile);

                    await DeleteLog.SendMessageAsync(msgBuilder);
                }
            }
            var DeletedMSG = _messageList.FirstOrDefault(w => w.Timestamp.Equals(e.Message.Timestamp) && w.Content.Equals(e.Message.Content));
            if (DeletedMSG != null)
            {
                _messageList.Remove(DeletedMSG);
            }
        }

        public static async Task Bulk_Delete_Log(DiscordClient Client, MessageBulkDeleteEventArgs e)
        {
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id
                                 select ss).FirstOrDefault();
            if (GuildSettings == null || GuildSettings.Delete_Log == 0) return;
            DiscordGuild Guild = Client.Guilds.FirstOrDefault(w => w.Value.Id == GuildSettings.ID_Server).Value;
            DiscordChannel DeleteLog = Guild.GetChannel(GuildSettings.Delete_Log);
            StringBuilder sb = new();
            foreach (var message in e.Messages.Reverse())
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
                await DeleteLog.SendMessageAsync(embed: embed);
            }
            else
            {
                File.WriteAllText($"{Program.TmpLoc}{e.Messages.Count}-BulkDeleteLog.txt", sb.ToString());
                using var upFile = new FileStream($"{Program.TmpLoc}{e.Messages.Count}-BulkDeleteLog.txt", FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                var msgBuilder = new DiscordMessageBuilder
                {
                    Content = $"Bulk delete log(Over the message cap) ({e.Messages.Count}) [{e.Messages[0].Timestamp} - {e.Messages[e.Messages.Count - 1].Timestamp}]"
                };
                msgBuilder.AddFile(upFile);
                await DeleteLog.SendMessageAsync(msgBuilder);
            }
        }

        public static async Task User_Join_Log(DiscordClient Client, GuildMemberAddEventArgs e)
        {
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id
                                 select ss).FirstOrDefault();
            DiscordGuild Guild = Client.Guilds.FirstOrDefault(w => w.Value.Id == GuildSettings.ID_Server).Value;
            if (GuildSettings == null || GuildSettings.User_Traffic == 0) return;
            DiscordChannel UserTraffic = Guild.GetChannel(GuildSettings.User_Traffic);
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
            await UserTraffic.SendMessageAsync(embed: embed);
        }

        public static async Task User_Leave_Log(DiscordClient Client, GuildMemberRemoveEventArgs e)
        {
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id
                                 select ss).FirstOrDefault();
            if (GuildSettings == null || GuildSettings.User_Traffic == 0) return;
            DiscordGuild Guild = Client.Guilds.FirstOrDefault(w => w.Value.Id == GuildSettings.ID_Server).Value;
            DiscordChannel UserTraffic = Guild.GetChannel(GuildSettings.User_Traffic);
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
            await UserTraffic.SendMessageAsync(embed: embed);
        }

        public static async Task User_Kicked_Log(DiscordClient Client, GuildMemberRemoveEventArgs e)
        {
            DateTimeOffset time = DateTimeOffset.UtcNow;
            DateTimeOffset beforetime = time.AddSeconds(-5);
            DateTimeOffset aftertime = time.AddSeconds(10);
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id
                                 select ss).FirstOrDefault();
            if (GuildSettings == null || GuildSettings.WKB_Log == 0) return;
            DiscordGuild Guild = Client.Guilds.FirstOrDefault(w => w.Value.Id == GuildSettings.ID_Server).Value;
            var logs = await Guild.GetAuditLogsAsync(5, action_type: AuditLogActionType.Kick);
            DiscordChannel wkbLog = Guild.GetChannel(GuildSettings.WKB_Log);
            if (logs.Count == 0) return;
            if (logs[0].CreationTimestamp >= beforetime && logs[0].CreationTimestamp <= aftertime)
            {
                await CustomMethod.SendModLogAsync(wkbLog, e.Member, $"*by {logs[0].UserResponsible.Mention}*\n**Reason:** {logs[0].Reason}", CustomMethod.ModLogType.Kick);

                var UserSettings = DB.DBLists.ServerRanks.FirstOrDefault(f => e.Member.Id == f.User_ID);
                if (UserSettings is null)
                {
                    Services.LeaderboardService.AddToServerLeaderboard(e.Member, e.Guild);
                    UserSettings = DB.DBLists.ServerRanks.First(f => e.Member.Id == f.User_ID && e.Guild.Id == f.Server_ID);
                }
                UserSettings.Kick_Count++;
                DB.DBLists.UpdateServerRanks(UserSettings);
                DB.DBLists.InsertWarnings(new DB.Warnings { Reason = logs[0].Reason, Active = false, Time_Created = DateTime.UtcNow, Admin_ID = logs[0].UserResponsible.Id, User_ID = e.Member.Id, Server_ID = e.Guild.Id, Type = "kick" });
            }
        }

        public static Task User_Banned_Log(DiscordClient Client, GuildBanAddEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                var wkb_Settings = DB.DBLists.ServerSettings.FirstOrDefault(w => w.ID_Server == e.Guild.Id);
                DiscordGuild Guild = Client.Guilds.FirstOrDefault(w => w.Key == wkb_Settings.ID_Server).Value;
                if (wkb_Settings.WKB_Log != 0)
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
                    DiscordChannel wkbLog = Guild.GetChannel(wkb_Settings.WKB_Log);
                    if (banEntry != null)
                    {
                        Console.WriteLine("Ban reason search succeeded");
                        await CustomMethod.SendModLogAsync(wkbLog, banEntry.Target, $"**User Banned:**\t{banEntry.Target.Mention}\n*by {banEntry.UserResponsible.Mention}*\n**Reason:** {banEntry.Reason}", CustomMethod.ModLogType.Ban);
                        DB.DBLists.InsertWarnings(new DB.Warnings { Reason = banEntry.Reason ?? "No reason specified", Active = false, Time_Created = DateTime.UtcNow, Admin_ID = banEntry.UserResponsible.Id, User_ID = banEntry.Target.Id, Server_ID = e.Guild.Id, Type = "ban" });
                    }
                    else
                    {
                        Console.WriteLine("Ban Reason search failed");
                        await wkbLog.SendMessageAsync("A user got banned but failed to find data, please log manually");
                    }
                }
                var UserSettings = DB.DBLists.ServerRanks.FirstOrDefault(f => e.Member.Id == f.User_ID && e.Guild.Id == f.Server_ID);
                if (UserSettings == null)
                {
                    DiscordUser user = await Client.GetUserAsync(e.Member.Id);
                    Services.LeaderboardService.AddToServerLeaderboard(user, e.Guild);
                    UserSettings = DB.DBLists.ServerRanks.FirstOrDefault(f => e.Member.Id == f.User_ID && e.Guild.Id == f.Server_ID);
                }
                UserSettings.Ban_Count += 1;
                DB.DBLists.UpdateServerRanks(UserSettings);
            });
            return Task.CompletedTask;
        }

        public static Task User_Unbanned_Log(DiscordClient Client, GuildBanRemoveEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                var wkb_Settings = (from ss in DB.DBLists.ServerSettings
                                    where ss.ID_Server == e.Guild.Id
                                    select ss).ToList();
                DiscordGuild Guild = await Client.GetGuildAsync(wkb_Settings[0].ID_Server);
                if (wkb_Settings[0].WKB_Log != 0)
                {
                    await Task.Delay(1000);
                    var logs = await Guild.GetAuditLogsAsync(1, action_type: AuditLogActionType.Unban);
                    DiscordChannel wkbLog = Guild.GetChannel(wkb_Settings[0].WKB_Log);
                    await CustomMethod.SendModLogAsync(wkbLog, e.Member, $"**User Unbanned:**\t{e.Member.Mention}\n*by {logs[0].UserResponsible.Mention}*", CustomMethod.ModLogType.Unban);
                }
            });
            return Task.CompletedTask;
        }

        public async Task Spam_Protection(object o, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild == null) return;
            ServerSettings serverSettings = DBLists.ServerSettings.FirstOrDefault(w => w.ID_Server == e.Guild.Id);

            if (serverSettings == null || serverSettings.WKB_Log == 0 || serverSettings.Spam_Exception_Channels.Any(id => id == e.Channel.Id)) return;
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

            int infractionLevel = DBLists.Warnings.Count(w => w.User_ID == member.Id && w.Server_ID == e.Guild.Id && w.Type == "warning" && w.Active);

            if (infractionLevel < 5)
            {
                _warningService.QueueWarning(e.Author, Program.Client.CurrentUser, e.Guild, e.Channel, $"Spam protection triggered - flood", true);
            }
        }

        public async Task Link_Spam_Protection(DiscordClient Client, MessageCreateEventArgs e)
        {
            var Server_Settings = (from ss in DB.DBLists.ServerSettings
                                   where ss.ID_Server == e.Guild?.Id
                                   select ss).FirstOrDefault();
            if (e.Author.IsBot || Server_Settings == null || Server_Settings.WKB_Log == 0 || !Server_Settings.HasLinkProtection) return;
            var invites = await e.Guild.GetInvitesAsync();
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (!CustomMethod.CheckIfMemberAdmin(member) && (e.Message.Content.Contains("discordapp.com/invite/") || e.Message.Content.Contains("discord.gg/")) && !invites.Any(w => e.Message.Content.Contains($"/{w.Code}")))
            {
                await e.Message.DeleteAsync();
                await member.TimeoutAsync(DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
                _warningService.QueueWarning(e.Author, Client.CurrentUser, e.Guild, e.Channel, $"Spam protection triggered - invite links", true);
            }
        }

        public async Task Everyone_Tag_Protection(DiscordClient Client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot || e.Guild == null) return;

            var Server_Settings = DB.DBLists.ServerSettings.FirstOrDefault(w=>w.ID_Server==e.Guild.Id);
            DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
            if (
                    Server_Settings != null &&
                    Server_Settings.WKB_Log != 0 &&
                    Server_Settings.HasEveryoneProtection &&
                    !member.Permissions.HasPermission(Permissions.MentionEveryone) &&
                    e.Message.Content.Contains("@everyone") &&
                    !EveryoneTagRegex().IsMatch(e.Message.Content)
                )
            {
                bool msgDeleted = false;
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
                    _warningService.QueueWarning(e.Author, Client.CurrentUser, e.Guild, e.Channel, $"Tried to tag everyone", true);
                }
            }
        }

        public static async Task Voice_Activity_Log(object Client, VoiceStateUpdateEventArgs e)
        {
            DB.ServerSettings SS = DB.DBLists.ServerSettings.FirstOrDefault(w => w.ID_Server == e.Guild.Id);

            if (SS.VCLog == 0) return;
            DiscordChannel VCActivityLogChannel = e.Guild.GetChannel(SS.VCLog);
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
                await VCActivityLogChannel.SendMessageAsync(embed);
            }
        }

        public static async Task User_Timed_Out_Log(object Client, GuildMemberUpdateEventArgs e)
        {
            if (e.Member.IsBot) return;
            DB.ServerSettings SS = DB.DBLists.ServerSettings.First(w => w.ID_Server == e.Guild.Id);
            if (SS.WKB_Log == 0) return;

            if (e.CommunicationDisabledUntilBefore == e.CommunicationDisabledUntilAfter) return;
            DiscordChannel UserTimedOutLogChannel = e.Guild.GetChannel(SS.WKB_Log);

            DateTimeOffset dto = e.Member.CommunicationDisabledUntil.GetValueOrDefault();
            if (e.CommunicationDisabledUntilAfter != null && e.CommunicationDisabledUntilBefore == null)
            {
                await CustomMethod.SendModLogAsync(UserTimedOutLogChannel, e.Member, $"**Timed Out Until:** <t:{dto.ToUnixTimeSeconds()}:F>(<t:{dto.ToUnixTimeSeconds()}:R>)", CustomMethod.ModLogType.TimedOut);
            }
            else if (e.CommunicationDisabledUntilAfter == null && e.CommunicationDisabledUntilBefore != null)
            {
                await CustomMethod.SendModLogAsync(UserTimedOutLogChannel, e.Member, $"**Timeout Removed**", CustomMethod.ModLogType.TimeOutRemoved);
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