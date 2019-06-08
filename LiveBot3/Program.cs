﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveBot
{
    internal class Program
    {
        public static DiscordClient Client { get; set; }
        public CommandsNextExtension Commands { get; set; }
        public static DateTime start = DateTime.Now;
        public static string BotVersion = $"20190608_B";

        // numbers
        public int StreamCheckDelay = 5;

        //lists
        public List<LiveStreamer> LiveStreamerList = new List<LiveStreamer>();

        public List<LevelTimer> UserLevelTimer = new List<LevelTimer>();
        public List<ServerLevelTimer> ServerUserLevelTimer = new List<ServerLevelTimer>();

        //channels
        public DiscordChannel TC1Photomode;

        public DiscordChannel TC2Photomode;
        public DiscordChannel deletelog;
        public DiscordChannel modlog;
        public DiscordChannel TCWelcome;
        public DiscordChannel testchannel;

        // guild
        public DiscordGuild TCGuild;

        public DiscordGuild testserver;

        //roles
        public DiscordRole Anonymous;

        private static void Main(string[] args)
        {
            var prog = new Program();
            prog.RunBotAsync(args).GetAwaiter().GetResult();
        }

        public async Task RunBotAsync(string[] args)
        {
            DataBase.DataBaseStart();
            DataBase.GetReactionRoles();
            var json = "";
            using (var fs = File.OpenRead("ConfigDev.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();
            if (args.Length == 1)
            {
                if (args[0] == "live")
                {
                    using (var fs = File.OpenRead("ConfigLive.json"))
                    using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                        json = await sr.ReadToEndAsync();
                    Console.WriteLine($"Running live version: {BotVersion}");
                }
            }
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            var cfg = new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };

            Client = new DiscordClient(cfg);
            Client.Ready += this.Client_Ready;
            Client.GuildAvailable += this.Client_GuildAvailable;
            Client.ClientErrored += this.Client_ClientError;

            var ccfg = new CommandsNextConfiguration
            {
                StringPrefixes = new string[] { cfgjson.CommandPrefix },
                CaseSensitive = false
            };
            this.Commands = Client.UseCommandsNext(ccfg);

            this.Commands.CommandExecuted += this.Commands_CommandExecuted;
            this.Commands.CommandErrored += this.Commands_CommandErrored;

            this.Commands.RegisterCommands<UngroupedCommands>();
            this.Commands.RegisterCommands<BotCMD1Commands>();
            this.Commands.RegisterCommands<OwnerCommands>();
            // Servers
            TCGuild = await Client.GetGuildAsync(150283740172517376); //The Crew server
            DiscordGuild SPGuild = await Client.GetGuildAsync(325271225565970434); // Star Player server
            testserver = await Client.GetGuildAsync(282478449539678210); //test server
            DiscordGuild SavaGuild = await Client.GetGuildAsync(311533687756029953); // savas server
            DiscordGuild test = await Client.GetGuildAsync(132671445376565248); // test

            // Channels
            TCWelcome = TCGuild.GetChannel(430006884834213888);
            TC1Photomode = TCGuild.GetChannel(191567033064751104);
            TC2Photomode = TCGuild.GetChannel(447134224349134848);
            deletelog = TCGuild.GetChannel(468315255986978816); // tc delete-log channel
            modlog = TCGuild.GetChannel(440365270893330432); // tc modlog
            // Roles - TC
            Anonymous = TCGuild.GetRole(257865859823828995);
            // roles - sava
            //
            testchannel = testserver.GetChannel(438354175668256768);
            //List<LiveStreamer> LiveStreamerList = new List<LiveStreamer>();
            //*/
            void StreamListCheck(List<LiveStreamer> list)
            {
                try
                {
                    foreach (var item in list)
                    {
                        if (item.Time.AddHours(StreamCheckDelay) < DateTime.Now && item.User.Presence.Activity.ActivityType != ActivityType.Streaming)
                        {
                            Console.WriteLine($"{item.User.Username} removed for time out");
                            list.Remove(item);
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("list empty");
                }
            }
            Timer StreamTimer = new Timer(e => StreamListCheck(LiveStreamerList), null, TimeSpan.Zero, TimeSpan.FromMinutes(2));
            //*/ comment this when testing features
            Client.PresenceUpdated += this.Presence_Updated;
            Client.MessageCreated += this.Message_Created;
            Client.MessageReactionAdded += this.Reaction_Role_Added;
            Client.MessageReactionRemoved += this.Reaction_Roles_Removed;
            Client.MessageDeleted += this.Message_Deleted;
            Client.GuildMemberAdded += this.Member_Joined;
            Client.GuildMemberRemoved += this.Memmber_Leave;
            Client.GuildBanAdded += this.Ban_Counter;
            //*/
            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        private async Task Presence_Updated(PresenceUpdateEventArgs e)
        {
            foreach (DataRow row in DataBase.StreamNotificationSettings.Rows)
            {
                DiscordGuild guild = await Client.GetGuildAsync(Convert.ToUInt64(row["server_id"].ToString()));
                DiscordChannel channel = guild.GetChannel(Convert.ToUInt64(row["channel_id"].ToString()));
                if (e.User.Presence.Guild.Id == guild.Id)
                {
                    LiveStreamer streamer = new LiveStreamer
                    {
                        User = e.User,
                        Time = DateTime.Now,
                        Guild = guild
                    };
                    int ItemIndex;
                    try
                    {
                        ItemIndex = LiveStreamerList.FindIndex(a => a.User.Id == e.User.Id && a.Guild.Id == e.User.Presence.Guild.Id);
                    }
                    catch (Exception)
                    {
                        ItemIndex = -1;
                    }
                    if (ItemIndex >= 0 && e.Activity.ActivityType != ActivityType.Streaming)
                    {
                        if (LiveStreamerList[ItemIndex].Time.AddHours(StreamCheckDelay) < DateTime.Now && e.Activity == LiveStreamerList[ItemIndex].User.Presence.Activity)
                        {
                            Console.WriteLine($"{ItemIndex} {LiveStreamerList[ItemIndex].User.Username} stoped streaming, removing from list");
                            LiveStreamerList.RemoveAt(ItemIndex);
                        }
                        else
                        {
                            Console.WriteLine($"{ItemIndex} {LiveStreamerList[ItemIndex].User.Username} {StreamCheckDelay} hours haven't passed. Not removing from list.");
                        }
                    }
                    else if (ItemIndex == -1 && e.Activity.ActivityType == ActivityType.Streaming)
                    {
                        DiscordMember StreamMember = await guild.GetMemberAsync(e.User.Id);
                        bool role = false, game = false;
                        if (row["roles_id"] != DBNull.Value)
                        {
                            foreach (DiscordRole urole in StreamMember.Roles)
                            {
                                foreach (string roleid in (string[])row["roles_id"])
                                {
                                    if (urole.Id.ToString() == roleid)
                                    {
                                        role = true;
                                    }
                                }
                            }
                        }
                        else if (row["roles_id"] == DBNull.Value)
                        {
                            role = true;
                        }
                        if (row["games"] != DBNull.Value)
                        {
                            foreach (string ugame in (string[])row["games"])
                            {
                                if (e.User.Presence.Activity.RichPresence.Details == ugame)
                                {
                                    game = true;
                                }
                            }
                        }
                        else if (row["games"] == DBNull.Value)
                        {
                            game = true;
                        }
                        if (game == true && role == true)
                        {
                            DiscordEmbedBuilder embed = new DiscordEmbedBuilder
                            {
                                Color = new DiscordColor(0x6441A5),
                                Author = new DiscordEmbedBuilder.EmbedAuthor
                                {
                                    IconUrl = e.User.AvatarUrl,
                                    Name = "STREAM",
                                    Url = e.User.Presence.Activity.StreamUrl
                                },
                                Description = $"**Streamer:**\n {e.User.Mention}\n\n" +
                        $"**Game:**\n{e.User.Presence.Activity.RichPresence.Details}\n\n" +
                        $"**Stream title:**\n{e.User.Presence.Activity.Name}\n\n" +
                        $"**Stream Link:**\n{e.User.Presence.Activity.StreamUrl}",
                                ThumbnailUrl = e.User.AvatarUrl,
                                Title = $"Check out {e.User.Username} is now Streaming!"
                            };
                            await channel.SendMessageAsync(embed: embed);
                            LiveStreamerList.Add(streamer);
                            Console.WriteLine($"User added to streaming list - {e.User.Username}");
                        }
                    }
                    else if (ItemIndex >= 0)
                    {
                        Console.WriteLine($"{ItemIndex} {LiveStreamerList[ItemIndex].User.Username} game changed");
                    }
                }
            }
        }

        private async Task Message_Created(MessageCreateEventArgs e)
        {
            if ((e.Channel == TC1Photomode || e.Channel == TC2Photomode) && !e.Author.IsBot) // deletes regular messages in photo mode channels
            {
                if (e.Message.Attachments.Count == 0)
                {
#pragma warning disable IDE0059 // Value assigned to symbol is never used
                    if (Uri.TryCreate(e.Message.Content, UriKind.Absolute, out Uri uri))
#pragma warning restore IDE0059 // Value assigned to symbol is never used
                    {
                    }
                    else
                    {
                        await e.Message.DeleteAsync();
                        DiscordMessage m = await e.Channel.SendMessageAsync("This channel is for sharing images only, please use the content comment channel for discussions. If this is a mistake please contact a moderator.");
                        await Task.Delay(9000);
                        await m.DeleteAsync();
                    }
                }
            }
            if (!e.Author.IsBot)
            {
                bool checkglobal = false, checklocal = false, update = false;
                Random r = new Random();
                int MinInterval = 10, MaxInterval = 30, MinMoney = 5, MaxMoney = 25;
                int points_added = r.Next(MinInterval, MaxInterval);
                int money_added = r.Next(MinMoney, MaxMoney);
                foreach (var Guser in UserLevelTimer)
                {
                    if (Guser.User.Id == e.Author.Id)
                    {
                        checkglobal = true;
                        if (Guser.Time.AddMinutes(2) <= DateTime.Now)
                        {
                            DataRow[] global = DataBase.Leaderboard.Select($"id_user='{e.Author.Id}'");
                            global[0]["followers"] = (long)global[0]["followers"] + points_added;
                            global[0]["bucks"] = (long)global[0]["bucks"] + money_added;
                            if ((int)global[0]["level"] < (long)global[0]["followers"] / (300 * ((int)global[0]["level"] + 1) * 0.5))
                            {
                                global[0]["level"] = (int)global[0]["level"] + 1;
                            }
                            Guser.Time = DateTime.Now;
                            update = true;
                        }
                    }
                }
                foreach (var Suser in ServerUserLevelTimer)
                {
                    if (Suser.User.Id == e.Author.Id)
                    {
                        if (Suser.Guild.Id == e.Guild.Id)
                        {
                            checklocal = true;
                            if (Suser.Time.AddMinutes(2) <= DateTime.Now)
                            {
                                DataRow[] local = DataBase.Server_Leaderboard.Select($"user_id='{e.Author.Id}' and server_id='{e.Channel.Guild.Id}'");
                                local[0]["followers"] = (long)local[0]["followers"] + points_added;
                                Suser.Time = DateTime.Now;
                                update = true;
                            }
                        }
                    }
                }
                if (!checkglobal)
                {
                    DataRow[] global = DataBase.Leaderboard.Select($"id_user='{e.Author.Id}'");
                    if (global.Length == 0)
                    {
                        DataRow newrow = DataBase.Leaderboard.NewRow();
                        newrow["id_user"] = e.Author.Id.ToString();
                        newrow["followers"] = 0;
                        newrow["level"] = 0;
                        newrow["bucks"] = 0;
                        DataBase.Leaderboard.Rows.Add(newrow);
                    }
                    global = DataBase.Leaderboard.Select($"id_user='{e.Author.Id}'");
                    points_added = r.Next(MinInterval, MaxInterval);
                    global[0]["followers"] = (long)global[0]["followers"] + points_added;
                    global[0]["bucks"] = (long)global[0]["bucks"] + money_added;
                    if ((int)global[0]["level"] < (long)global[0]["followers"] / (300 * ((int)global[0]["level"] + 1) * 0.5))
                    {
                        global[0]["level"] = (int)global[0]["level"] + 1;
                    }
                    LevelTimer NewToList = new LevelTimer
                    {
                        Time = DateTime.Now,
                        User = e.Author
                    };
                    UserLevelTimer.Add(NewToList);
                    update = true;
                }
                if (!checklocal)
                {
                    DataRow[] local = DataBase.Server_Leaderboard.Select($"user_id='{e.Author.Id}' and server_id='{e.Channel.Guild.Id}'");
                    if (local.Length == 0)
                    {
                        DataRow newrow = DataBase.Server_Leaderboard.NewRow();
                        newrow["user_id"] = e.Author.Id.ToString();
                        newrow["server_id"] = e.Channel.Guild.Id.ToString();
                        newrow["followers"] = 0;
                        DataBase.Server_Leaderboard.Rows.Add(newrow);
                    }
                    local = DataBase.Server_Leaderboard.Select($"user_id='{e.Author.Id}' and server_id='{e.Channel.Guild.Id}'");
                    local[0]["followers"] = (long)local[0]["followers"] + points_added;
                    ServerLevelTimer NewToList = new ServerLevelTimer
                    {
                        Time = DateTime.Now,
                        User = e.Author,
                        Guild = e.Guild
                    };
                    ServerUserLevelTimer.Add(NewToList);
                    update = true;
                }
                if (update == true)
                {
                    DataBase.UpdateLeaderboards("base");
                }
            }
        }

        private async Task Reaction_Role_Added(MessageReactionAddEventArgs e)
        {
            if (e.Emoji.Id != 0)
            {
                DataTable dt = DataBase.Reaction_Roles_DT;
                DiscordEmoji used = e.Emoji;
                DiscordMessage sourcemsg = e.Message;
                DiscordUser username = e.User;
                //ulong f = e.User.Id;
                DataRow[] result = dt.Select($"server_id={e.Channel.Guild.Id.ToString()} AND message_id={sourcemsg.Id.ToString()} AND reaction_id={used.Id.ToString()}");
                if (result.Length == 1)
                {
                    DiscordGuild guild = await Client.GetGuildAsync(UInt64.Parse(result[0]["server_id"].ToString()));
                    DiscordMember rolemember = await guild.GetMemberAsync(username.Id);
                    await rolemember.GrantRoleAsync(guild.GetRole(UInt64.Parse(result[0]["role_id"].ToString())));
                }
            }
        }

        private async Task Reaction_Roles_Removed(MessageReactionRemoveEventArgs e)
        {
            if (e.Emoji.Id != 0)
            {
                DataTable dt = DataBase.Reaction_Roles_DT;
                DiscordEmoji used = e.Emoji;
                DiscordMessage sourcemsg = e.Message;
                DiscordUser username = e.User;
                //ulong f = e.User.Id;
                DataRow[] result = dt.Select($"server_id={e.Channel.Guild.Id.ToString()} AND message_id={sourcemsg.Id.ToString()} AND reaction_id={used.Id.ToString()}");
                if (result.Length == 1)
                {
                    DiscordGuild guild = await Client.GetGuildAsync(UInt64.Parse(result[0]["server_id"].ToString()));
                    DiscordMember rolemember = await guild.GetMemberAsync(username.Id);
                    await rolemember.RevokeRoleAsync(guild.GetRole(UInt64.Parse(result[0]["role_id"].ToString())));
                }
            }
        }

        private async Task Message_Deleted(MessageDeleteEventArgs e)
        {
            DiscordMessage msg = e.Message;
            DiscordUser author = msg.Author;
            if (e.Guild == TCGuild)
            {
                if (author.IsBot == false)
                {
                    string converteddeletedmsg = msg.Content;
                    if (converteddeletedmsg.StartsWith("/"))
                    {
                        DiscordEmbedBuilder embed = new DiscordEmbedBuilder
                        {
                            Color = new DiscordColor(0xFF6600),
                            Author = new DiscordEmbedBuilder.EmbedAuthor
                            {
                                IconUrl = author.AvatarUrl,
                                Name = author.Username
                            },
                            Description = $"Command initialization was deleted in {e.Channel.Mention}\n" +
                            $"**Author:** {author.Username}\t ID:{author.Id}\n" +
                            $"**Content:** {converteddeletedmsg}\n" +
                            $"**Time Posted:** {msg.CreationTimestamp}"
                        };
                        await deletelog.SendMessageAsync(embed: embed);
                    }
                    else
                    {
                        if (converteddeletedmsg == "")
                        {
                            converteddeletedmsg = "*message didn't contain any text, probably file*";
                        }

                        DiscordEmbedBuilder embed = new DiscordEmbedBuilder
                        {
                            Color = new DiscordColor(0xFF6600),
                            Author = new DiscordEmbedBuilder.EmbedAuthor
                            {
                                IconUrl = author.AvatarUrl,
                                Name = author.Username
                            },
                            Description = $"{author.Mention}'s message was deleted in {e.Channel.Mention}\n" +
                            $"**Contents:** {converteddeletedmsg}\n" +
                            $"Time posted: {msg.CreationTimestamp}"
                        };
                        await deletelog.SendMessageAsync(embed: embed);
                    }
                }
            }
        }

        private async Task Member_Joined(GuildMemberAddEventArgs e)
        {
            if (e.Guild == TCGuild)
            {
                string name = e.Member.Username;
                if (name.Contains("discord.gg/") || name.Contains("discordapp.com/invite/"))
                {
                    try
                    {
                        await e.Member.SendMessageAsync("Your nickname contains a server invite thus you have been removed.");
                    }
                    catch
                    {
                    }
                    await e.Member.BanAsync();
                    await modlog.SendMessageAsync($"{e.Member.Mention} Was baned for having an invite link in their name.");
                }
                else
                {
                    await TCWelcome.SendMessageAsync($"Welcome {e.Member.Mention} to The Crew Community Discord! " + File.ReadAllText(@"TextFiles/sys/welcome.txt"));
                    try
                    {
                        await e.Member.GrantRoleAsync(Anonymous);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private async Task Memmber_Leave(GuildMemberRemoveEventArgs e)
        {
            DateTimeOffset time = DateTimeOffset.Now.UtcDateTime;
            DateTimeOffset beforetime = time.AddSeconds(-5);
            DateTimeOffset aftertime = time.AddSeconds(10);
            DataBase.UpdateTables();
            DataTable User_warnings = DataBase.User_warnings;
            string uid = e.Member.Id.ToString();
            bool UserCheck = false;
            if (e.Guild == TCGuild)
            {
                string name = e.Member.Username;
                if (name.Contains("discord.gg/") || name.Contains("discordapp.com/invite/"))
                {
                }
                else
                {
                    await TCWelcome.SendMessageAsync($"{name} " + File.ReadAllText(@"TextFiles/sys/bye.txt"));
                }

                // Checks if user was kicked.
                var logs = await testserver.GetAuditLogsAsync(1, action_type: AuditLogActionType.Kick);
                foreach (var item in logs)
                {
                    if (item.CreationTimestamp >= beforetime && item.CreationTimestamp <= aftertime)
                    {
                        foreach (DataRow rows in User_warnings.Rows)
                        {
                            if (rows["id_user"].ToString() == uid)
                            {
                                UserCheck = true;
                                rows["kick_count"] = (int)rows["kick_count"] + 1;
                            }
                        }
                        if (!UserCheck)
                        {
                            Console.WriteLine("kicked");
                            DataRow NewUser = User_warnings.NewRow();
                            NewUser["warning_level"] = 0;
                            NewUser["warning_count"] = 0;
                            NewUser["kick_count"] = 1;
                            NewUser["ban_count"] = 0;
                            NewUser["id_user"] = uid;
                            User_warnings.Rows.Add(NewUser);
                        }
                        DataBase.UserWarnAdapter.Update(User_warnings);
                    }
                }
            }
        }

        private async Task Ban_Counter(GuildBanAddEventArgs e)
        {
            DataBase.UpdateTables();
            DataTable User_warnings = DataBase.User_warnings;
            string uid = e.Member.Id.ToString();
            bool UserCheck = false;
            if (e.Guild == TCGuild)
            {
                foreach (DataRow rows in User_warnings.Rows)
                {
                    if (rows["id_user"].ToString() == uid)
                    {
                        UserCheck = true;
                        rows["ban_count"] = (int)rows["ban_count"] + 1;
                    }
                }
                if (!UserCheck)
                {
                    DataRow NewUser = User_warnings.NewRow();
                    NewUser["warning_level"] = 0;
                    NewUser["warning_count"] = 0;
                    NewUser["kick_count"] = 0;
                    NewUser["ban_count"] = 1;
                    NewUser["id_user"] = uid;
                    User_warnings.Rows.Add(NewUser);
                }
                DataBase.UserWarnAdapter.Update(User_warnings);
            }
            await Task.Delay(0);
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "LiveBot", "Client is ready to process events.", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "LiveBot", $"Guild available: {e.Guild.Name}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "LiveBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "LiveBot", $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'", DateTime.Now);
            return Task.CompletedTask;
        }

        private async Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Error, "LiveBot", $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}", DateTime.Now);
#pragma warning disable IDE0059 // Value assigned to symbol is never used
            if (e.Exception is ChecksFailedException ex)
#pragma warning restore IDE0059 // Value assigned to symbol is never used
            {
                var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You do not have the permissions required to execute this command.",
                    Color = new DiscordColor(0xFF0000) // red
                };
                await e.Context.RespondAsync("", embed: embed);
            }
        }
    }

    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("prefix")]
        public string CommandPrefix { get; private set; }
    }

    public struct DBJson
    {
        [JsonProperty("host")]
        public string Host { get; private set; }

        [JsonProperty("username")]
        public string Username { get; private set; }

        [JsonProperty("password")]
        public string Password { get; private set; }

        [JsonProperty("database")]
        public string Database { get; private set; }
    }

    public struct TCEJson
    {
        [JsonProperty("key")]
        public string Key { get; private set; }
    }

    internal class LiveStreamer
    {
        public DiscordUser User { get; set; }
        public DateTime Time { get; set; }
        public DiscordGuild Guild { get; set; }
    }

    internal class LevelTimer
    {
        public DiscordUser User { get; set; }
        public DateTime Time { get; set; }
    }

    internal class ServerLevelTimer
    {
        public DiscordUser User { get; set; }
        public DiscordGuild Guild { get; set; }
        public DateTime Time { get; set; }
    }
    public struct SummitJson
    {
        [JsonProperty("id")]
        public ulong Summit_ID { get; private set; }
        [JsonProperty("start_date")]
        public string Start_Date { get; private set; }
        [JsonProperty("ticket_short")]
        public string LinkEnd { get; private set; }
    }
    public struct EventJson
    {
        [JsonProperty("total_players")]
        public string Player_Count { get; private set; }
        [JsonProperty("tier_entries")]
        public Tier_Entries[] Tier_entries { get; private set; }
    }
    public class Tier_Entries
    {
        public ulong Points { get; set; }
        public ulong Rank { get; set; }
    }
}