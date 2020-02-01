using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Newtonsoft.Json;
using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveBot
{
    internal class Program
    {
        public static DiscordClient Client { get; set; }
        public InteractivityExtension Interactivity { get; set; }
        public CommandsNextExtension Commands { get; set; }
        public static DateTime start = DateTime.Now;
        public static string BotVersion = $"20200129_A";

        // numbers
        public int StreamCheckDelay = 5;

        // string

        public static string tmpLoc = Path.GetTempPath()+"/livebot-";

        //lists
        public List<LiveStreamer> LiveStreamerList = new List<LiveStreamer>();

        public List<ActivateRolesTimer> ActivateRolesTimer = new List<ActivateRolesTimer>();

        public List<LevelTimer> UserLevelTimer = new List<LevelTimer>();
        public List<ServerLevelTimer> ServerUserLevelTimer = new List<ServerLevelTimer>();
        public List<GuildTimer> RandomMsgTimer = new List<GuildTimer>();

        //channels
        public DiscordChannel TC1Photomode;

        public DiscordChannel TC2Photomode;

        // guild
        public static DiscordGuild TCGuild;

        // fonts
        public static FontCollection fonts = new FontCollection();

        private static void Main(string[] args)
        {
            var prog = new Program();
            prog.RunBotAsync(args).GetAwaiter().GetResult();
        }

        public async Task RunBotAsync(string[] args)
        {
            // fills all database lists
            DB.DBLists.LoadAllLists();
            fonts.Install("Assets/Fonts/Hurme_Geometric_Sans_3_W03_Blk.ttf");

            bool TestBuild = true;

            var json = "";
            using (var fs = File.OpenRead("Config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();
            Json.Bot cfgjson = JsonConvert.DeserializeObject<Json.Config>(json).DevBot;
            if (args.Length == 1)
            {
                if (args[0] == "live") // Checks for command argument to be "live", if so, then launches the live version of the bot, not dev
                {
                    cfgjson = JsonConvert.DeserializeObject<Json.Config>(json).LiveBot;
                    Console.WriteLine($"Running live version: {BotVersion}");
                    TestBuild = false;
                }
            }
            var cfg = new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                ReconnectIndefinitely = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };

            Client = new DiscordClient(cfg);
            Client.Ready += this.Client_Ready;
            Client.GuildAvailable += this.Client_GuildAvailable;
            Client.ClientErrored += this.Client_ClientError;

            Client.UseInteractivity(new InteractivityConfiguration
            {
                PaginationBehaviour = DSharpPlus.Interactivity.Enums.PaginationBehaviour.Ignore,
                Timeout = TimeSpan.FromMinutes(2)
            });

            var ccfg = new CommandsNextConfiguration
            {
                StringPrefixes = new string[] { cfgjson.CommandPrefix },
                CaseSensitive = false
            };
            this.Commands = Client.UseCommandsNext(ccfg);

            this.Commands.CommandExecuted += this.Commands_CommandExecuted;
            this.Commands.CommandErrored += this.Commands_CommandErrored;

            this.Commands.RegisterCommands<Commands.UngroupedCommands>();
            this.Commands.RegisterCommands<Commands.AdminCommands>();
            this.Commands.RegisterCommands<Commands.OCommands>();

            // Servers
            TCGuild = await Client.GetGuildAsync(150283740172517376); //The Crew server

            // Channels
            TC1Photomode = TCGuild.GetChannel(191567033064751104);
            TC2Photomode = TCGuild.GetChannel(447134224349134848);

            //*/
            Timer StreamTimer = new Timer(e => TimerMethod.StreamListCheck(LiveStreamerList, StreamCheckDelay), null, TimeSpan.Zero, TimeSpan.FromMinutes(2));
            Timer RoleTimer = new Timer(e => TimerMethod.ActivatedRolesCheck(ActivateRolesTimer), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            if (!TestBuild) //Only enables these when using live version
            {
                Client.PresenceUpdated += this.Presence_Updated;
                Client.MessageCreated += this.Message_Created;
                Client.MessageReactionAdded += this.Reaction_Role;
                Client.MessageDeleted += this.Message_Deleted;
                Client.GuildMemberAdded += this.Member_Joined;
                Client.GuildMemberRemoved += this.Memmber_Leave;
                Client.GuildBanAdded += this.Ban_Counter;
                Client.GuildBanRemoved += this.Ban_Removed;
            }

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        private async Task Presence_Updated(PresenceUpdateEventArgs e)
        {
            List<DB.StreamNotifications> StreamNotifications = DB.DBLists.StreamNotifications;
            foreach (var row in StreamNotifications)
            {
                DiscordGuild guild = await Client.GetGuildAsync(Convert.ToUInt64(row.Server_ID.ToString()));
                DiscordChannel channel = guild.GetChannel(Convert.ToUInt64(row.Channel_ID.ToString()));
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
                        ItemIndex = LiveStreamerList.FindIndex(a => a.User.Id == e.User.Id
                        && a.Guild.Id == e.User.Presence.Guild.Id);
                    }
                    catch (Exception)
                    {
                        ItemIndex = -1;
                    }
                    if (ItemIndex >= 0
                        && e.User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault() == null)
                    {
                        //removes user from list
                        if (LiveStreamerList[ItemIndex].Time.AddHours(StreamCheckDelay) < DateTime.Now
                            && e.User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault() == LiveStreamerList[ItemIndex].User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault())
                        {
                            LiveStreamerList.RemoveAt(ItemIndex);
                        }
                    }
                    else if (ItemIndex == -1
                        && e.User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault() != null
                        && e.User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault().ActivityType.Equals(ActivityType.Streaming))
                    {
                        DiscordMember StreamMember = await guild.GetMemberAsync(e.User.Id);
                        bool role = false, game = false;
                        string gameTitle = e.User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault().RichPresence.State;
                        string streamTitle = e.User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault().RichPresence.Details;
                        string streamURL = e.User.Presence.Activities.Where(w => w.Name.ToLower() == "twitch").FirstOrDefault().StreamUrl;
                        if (row.Roles_ID != null)
                        {
                            foreach (DiscordRole urole in StreamMember.Roles)
                            {
                                foreach (string roleid in (string[])row.Roles_ID)
                                {
                                    if (urole.Id.ToString() == roleid)
                                    {
                                        role = true;
                                    }
                                }
                            }
                        }
                        else if (row.Roles_ID == null)
                        {
                            role = true;
                        }
                        if (row.Games != null)
                        {
                            foreach (string ugame in row.Games)
                            {
                                try
                                {
                                    if (gameTitle == ugame)
                                    {
                                        game = true;
                                    }
                                }
                                catch { }
                            }
                        }
                        else if (row.Games == null)
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
                                    Url = streamURL
                                },
                                Description = $"**Streamer:**\n {e.User.Mention}\n\n" +
                        $"**Game:**\n{gameTitle}\n\n" +
                        $"**Stream title:**\n{streamTitle}\n\n" +
                        $"**Stream Link:**\n{streamURL}",
                                ThumbnailUrl = e.User.AvatarUrl,
                                Title = $"Check out {e.User.Username} is now Streaming!"
                            };
                            await channel.SendMessageAsync(embed: embed);
                            //adds user to list
                            LiveStreamerList.Add(streamer);
                        }
                    }
                }
            }
        }

        private async Task Message_Created(MessageCreateEventArgs e)
        {
            // deletes regular messages in photo mode channels
            if ((e.Channel == TC1Photomode || e.Channel == TC2Photomode) && !e.Author.IsBot)
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
            // Responds to TCE bot
            if (e.Author.IsBot && e.Author.Id.Equals(202440605123477505) && e.Message.Content.Equals("Hey Live!"))
            {
                await e.Channel.SendMessageAsync("Hey!");
            }
            // Leveling system
            if (!e.Author.IsBot)
            {
                DB.DBLists.LoadServerRanks();
                DB.DBLists.LoadLeaderboard();
                bool checkglobal = false, checklocal = false;
                Random r = new Random();
                int MinInterval = 10, MaxInterval = 30;
                if (e.Message.Content.Length > 500)
                {
                    MinInterval = 40;
                    MaxInterval = 70;
                }
                else if (e.Message.Content.Length > 100 && e.Message.Content.Length <= 500)
                {
                    MinInterval += (29 / 500) * e.Message.Content.Length;
                    MaxInterval += (29 / 500) * e.Message.Content.Length;
                }
                int MinMoney = 2, MaxMoney = 5;
                int points_added = r.Next(MinInterval, MaxInterval);
                int money_added = r.Next(MinMoney, MaxMoney);
                foreach (var Guser in UserLevelTimer)
                {
                    if (Guser.User.Id == e.Author.Id)
                    {
                        checkglobal = true;
                        if (Guser.Time.AddMinutes(2) <= DateTime.Now)
                        {
                            List<DB.Leaderboard> Leaderboard = DB.DBLists.Leaderboard;
                            var global = (from lb in Leaderboard
                                          where lb.ID_User == e.Author.Id.ToString()
                                          select lb).ToList();
                            global[0].Followers = (long)global[0].Followers + points_added;
                            global[0].Bucks = (long)global[0].Bucks + money_added;
                            if ((int)global[0].Level < (long)global[0].Followers / (300 * ((int)global[0].Level + 1) * 0.5))
                            {
                                global[0].Level = (int)global[0].Level + 1;
                            }
                            Guser.Time = DateTime.Now;
                            DB.DBLists.UpdateLeaderboard(global);
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
                                List<DB.ServerRanks> Leaderboard = DB.DBLists.ServerRanks;
                                var local = (from lb in Leaderboard
                                             where lb.User_ID == e.Author.Id.ToString()
                                             where lb.Server_ID == e.Channel.Guild.Id.ToString()
                                             select lb).ToList();
                                local[0].Followers = (long)local[0].Followers + points_added;
                                Suser.Time = DateTime.Now; DB.DBLists.UpdateServerRanks(local);
                            }
                        }
                    }
                }
                if (!checkglobal)
                {
                    List<DB.Leaderboard> Leaderboard = DB.DBLists.Leaderboard;
                    var global = (from lb in Leaderboard
                                  where lb.ID_User == e.Author.Id.ToString()
                                  select lb).ToList();
                    if (global.Count == 0)
                    {
                        CustomMethod.AddUserToLeaderboard(e.Author);
                    }
                    global = (from lb in Leaderboard
                              where lb.ID_User == e.Author.Id.ToString()
                              select lb).ToList();
                    points_added = r.Next(MinInterval, MaxInterval);
                    if (global.Count == 1)
                    {
                        global[0].Followers = (long)global[0].Followers + points_added;
                        global[0].Bucks = (long)global[0].Bucks + money_added;
                        if ((int)global[0].Level < (long)global[0].Followers / (300 * ((int)global[0].Level + 1) * 0.5))
                        {
                            global[0].Level = (int)global[0].Level + 1;
                        }
                    }
                    LevelTimer NewToList = new LevelTimer
                    {
                        Time = DateTime.Now,
                        User = e.Author
                    };
                    UserLevelTimer.Add(NewToList);
                    DB.DBLists.UpdateLeaderboard(global);
                }
                if (!checklocal)
                {
                    List<DB.ServerRanks> Leaderboard = DB.DBLists.ServerRanks;
                    CustomMethod.AddUserToServerRanks(e.Author, e.Guild);
                    var local = (from lb in Leaderboard
                                 where lb.User_ID == e.Author.Id.ToString()
                                 where lb.Server_ID == e.Channel.Guild.Id.ToString()
                                 select lb).ToList();
                    if (local.Count > 0)
                    {
                        local[0].Followers = (long)local[0].Followers + points_added;
                    }
                    ServerLevelTimer NewToList = new ServerLevelTimer
                    {
                        Time = DateTime.Now,
                        User = e.Author,
                        Guild = e.Guild
                    };
                    ServerUserLevelTimer.Add(NewToList);
                    DB.DBLists.UpdateServerRanks(local);
                }
                //*/
                var userrank = (from sr in DB.DBLists.ServerRanks
                                where sr.Server_ID == e.Guild.Id.ToString()
                                where sr.User_ID == e.Author.Id.ToString()
                                select sr).ToList()[0].Followers;
                var rankedroles = (from rr in DB.DBLists.RankRoles
                                   where rr.Server_Rank != 0
                                   where rr.Server_Rank <= userrank
                                   where rr.Server_ID == e.Guild.Id.ToString()
                                   select rr).ToList();
                List<DiscordRole> roles = new List<DiscordRole>();
                foreach (var item in rankedroles)
                {
                    if (item.Server_ID == e.Guild.Id.ToString())
                    {
                        roles.Add(e.Guild.GetRole(Convert.ToUInt64(item.Role_ID)));
                    }
                }
                DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
                for (int i = 0; i < roles.Count; i++)
                {
                    if (i != roles.Count - 1 && member.Roles.Contains(roles[i]))
                    {
                        await member.RevokeRoleAsync(roles[i]);
                    }
                    else if (i == roles.Count - 1 && !member.Roles.Contains(roles[i]))
                    {
                        await member.GrantRoleAsync(roles[i]);
                    }
                }
            }
            // Random message response
        }

        private async Task Reaction_Role(MessageReactionAddEventArgs e)
        {
            if (e.Emoji.Id != 0 && !e.User.IsBot)
            {
                DiscordEmoji used = e.Emoji;
                DiscordMessage sourcemsg = e.Message;
                DiscordUser username = e.User;

                List<DB.ReactionRoles> ReactionRoles = DB.DBLists.ReactionRoles;
                var RoleInfo = (from rr in ReactionRoles
                                where rr.Server_ID == e.Channel.Guild.Id.ToString()
                                where rr.Message_ID == sourcemsg.Id.ToString()
                                where rr.Reaction_ID == used.Id.ToString()
                                select rr).ToList();
                if (RoleInfo.Count == 1)
                {
                    DiscordGuild guild = await Client.GetGuildAsync(UInt64.Parse(RoleInfo[0].Server_ID.ToString()));
                    if (RoleInfo[0].Type == "acquire")
                    {
                        DiscordMember rolemember = await guild.GetMemberAsync(username.Id);
                        if (rolemember.Roles.Where(w => w.Id == UInt64.Parse(RoleInfo[0].Role_ID.ToString())).Count() > 0)
                        {
                            await rolemember.RevokeRoleAsync(guild.GetRole(UInt64.Parse(RoleInfo[0].Role_ID.ToString())));
                        }
                        else
                        {
                            await rolemember.GrantRoleAsync(guild.GetRole(UInt64.Parse(RoleInfo[0].Role_ID.ToString())));
                        }

                        await Task.Delay(5000).ContinueWith(t => sourcemsg.DeleteReactionAsync(used, e.User, null));
                    }
                    else if (RoleInfo[0].Type == "activate")
                    {
                        DiscordRole role = guild.GetRole(UInt64.Parse(RoleInfo[0].Role_ID.ToString()));
                        string msg = $"---";
                        if (role.IsMentionable)
                        {
                            await role.ModifyAsync(mentionable: false);
                            msg = $"{role.Name} ⨯";
                            ActivateRolesTimer.RemoveAt(ActivateRolesTimer.FindIndex(a => a.Guild == e.Guild && a.Role == role));
                        }
                        else if (!role.IsMentionable)
                        {
                            await role.ModifyAsync(mentionable: true);
                            msg = $"{role.Name} ✓";
                            ActivateRolesTimer newItem = new ActivateRolesTimer
                            {
                                Guild = guild,
                                Role = role,
                                Time = DateTime.Now
                            };
                            ActivateRolesTimer.Add(newItem);
                        }
                        await sourcemsg.DeleteReactionAsync(used, e.User, null);
                        DiscordMessage m = await e.Channel.SendMessageAsync(msg);
                        await Task.Delay(3000).ContinueWith(t => m.DeleteAsync());
                    }
                }
            }
        }

        private async Task Message_Deleted(MessageDeleteEventArgs e)
        {
            DiscordMessage msg = e.Message;
            DiscordUser author = msg.Author;
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id.ToString()
                                 select ss).ToList();
            if (GuildSettings[0].Delete_Log != "0")
            {
                DiscordGuild Guild = await Client.GetGuildAsync(Convert.ToUInt64(GuildSettings[0].ID_Server));
                DiscordChannel DeleteLog = Guild.GetChannel(Convert.ToUInt64(GuildSettings[0].Delete_Log));
                if (!author.IsBot)
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
                        await DeleteLog.SendMessageAsync(embed: embed);
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
                        await DeleteLog.SendMessageAsync(embed: embed);
                    }
                }
            }
        }

        private async Task Member_Joined(GuildMemberAddEventArgs e)
        {
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id.ToString()
                                 select ss).ToList();
            var JoinRole = (from rr in DB.DBLists.RankRoles
                            where rr.Server_ID == e.Guild.Id.ToString()
                            where rr.Server_Rank == 0
                            select rr).ToList();
            DiscordGuild Guild = await Client.GetGuildAsync(Convert.ToUInt64(GuildSettings[0].ID_Server));
            if (GuildSettings[0].User_Traffic != "0")
            {
                DiscordChannel UserTraffic = Guild.GetChannel(Convert.ToUInt64(GuildSettings[0].User_Traffic));
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder
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
            if (GuildSettings[0].Welcome_Settings[0] != "0")
            {
                DiscordChannel WelcomeChannel = Guild.GetChannel(Convert.ToUInt64(GuildSettings[0].Welcome_Settings[0]));
                if (GuildSettings[0].Welcome_Settings[1] != "0")
                {
                    string msg = GuildSettings[0].Welcome_Settings[1];
                    msg = msg.Replace("$Mention", $"{e.Member.Mention}");
                    await WelcomeChannel.SendMessageAsync(msg);
                    if (JoinRole.Count != 0)
                    {
                        DiscordRole role = Guild.GetRole(Convert.ToUInt64(JoinRole[0].Role_ID));
                        await e.Member.GrantRoleAsync(role);
                    }
                }
            }

            var global = (from lb in DB.DBLists.Leaderboard
                          where lb.ID_User == e.Member.Id.ToString()
                          select lb).ToList();
            if (global.Count == 0)
            {
                CustomMethod.AddUserToLeaderboard(e.Member);
            }
            var local = (from lb in DB.DBLists.ServerRanks
                         where lb.User_ID == e.Member.Id.ToString()
                         where lb.Server_ID == e.Guild.Id.ToString()
                         select lb).ToList();
            if (local.Count == 0)
            {
                CustomMethod.AddUserToServerRanks(e.Member, e.Guild);
            }
        }

        private async Task Memmber_Leave(GuildMemberRemoveEventArgs e)
        {
            DateTimeOffset time = DateTimeOffset.Now.UtcDateTime;
            DateTimeOffset beforetime = time.AddSeconds(-5);
            DateTimeOffset aftertime = time.AddSeconds(10);
            string uid = e.Member.Id.ToString();
            bool UserCheck = false;
            var GuildSettings = (from ss in DB.DBLists.ServerSettings
                                 where ss.ID_Server == e.Guild.Id.ToString()
                                 select ss).ToList();
            DiscordGuild Guild = await Client.GetGuildAsync(Convert.ToUInt64(GuildSettings[0].ID_Server));
            if (GuildSettings[0].User_Traffic != "0")
            {
                DiscordChannel UserTraffic = Guild.GetChannel(Convert.ToUInt64(GuildSettings[0].User_Traffic));
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder
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
            if (GuildSettings[0].Welcome_Settings[0] != "0")
            {
                DiscordChannel WelcomeChannel = Guild.GetChannel(Convert.ToUInt64(GuildSettings[0].Welcome_Settings[0]));
                if (GuildSettings[0].Welcome_Settings[2] != "0")
                {
                    string msg = GuildSettings[0].Welcome_Settings[2];
                    msg = msg.Replace("$Username", $"{e.Member.Username}");
                    await WelcomeChannel.SendMessageAsync(msg);
                }
            }
            var logs = await Guild.GetAuditLogsAsync(1, action_type: AuditLogActionType.Kick);
            if (GuildSettings[0].WKB_Log != "0")
            {
                DiscordChannel wkbLog = Guild.GetChannel(Convert.ToUInt64(GuildSettings[0].WKB_Log));
                if (logs[0].CreationTimestamp >= beforetime && logs[0].CreationTimestamp <= aftertime)
                {
                    DiscordEmbedBuilder embed = new DiscordEmbedBuilder
                    {
                        Title = $"👢 {e.Member.Username} ({e.Member.Id}) has been kicked",
                        Description = $"*by {logs[0].UserResponsible.Mention}*\n**Reason:** {logs[0].Reason}",
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            IconUrl = e.Member.AvatarUrl,
                            Text = $"User kicked"
                        },
                        Color = new DiscordColor(0xff0000),
                    };
                    await wkbLog.SendMessageAsync(embed: embed);
                }
            }
            // Checks if user was kicked.
            foreach (var item in logs)
            {
                if (item.CreationTimestamp >= beforetime && item.CreationTimestamp <= aftertime)
                {
                    var UserSettings = DB.DBLists.ServerRanks.FirstOrDefault(f => e.Member.Id.ToString().Equals(f.User_ID));
                    Console.WriteLine(UserSettings.Kick_Count);
                    UserCheck = true;
                    UserSettings.Kick_Count++;
                    Console.WriteLine(UserSettings.Kick_Count);
                    DB.DBLists.UpdateServerRanks(new List<DB.ServerRanks> { UserSettings });
                    if (!UserCheck)
                    {
                        DB.ServerRanks newEntry = new DB.ServerRanks
                        {
                            Server_ID = e.Guild.Id.ToString(),
                            Ban_Count = 0,
                            Kick_Count = 1,
                            Warning_Level = 0,
                            User_ID = uid
                        };
                        DB.DBLists.InsertServerRanks(newEntry);
                    }
                }
            }
        }

        private async Task Ban_Counter(GuildBanAddEventArgs e)
        {
            var wkb_Settings = (from ss in DB.DBLists.ServerSettings
                                where ss.ID_Server == e.Guild.Id.ToString()
                                select ss).ToList();
            DiscordGuild Guild = await Client.GetGuildAsync(Convert.ToUInt64(wkb_Settings[0].ID_Server));
            if (wkb_Settings[0].WKB_Log != "0")
            {
                await Task.Delay(2000);
                var logs = await Guild.GetAuditLogsAsync(1, action_type: AuditLogActionType.Ban);
                DiscordChannel wkbLog = Guild.GetChannel(Convert.ToUInt64(wkb_Settings[0].WKB_Log));
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder
                {
                    Title = $"❌ {e.Member.Username} ({e.Member.Id}) has been banned",
                    Description = $"*by {logs[0].UserResponsible.Mention}*\n**Reason:** {logs[0].Reason}",
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        IconUrl = e.Member.AvatarUrl,
                        Text = $"User banned"
                    },
                    Color = new DiscordColor(0xff0000),
                };
                await wkbLog.SendMessageAsync(embed: embed);
            }
            var UserSettings = DB.DBLists.ServerRanks.FirstOrDefault(f => e.Member.Id.ToString().Equals(f.User_ID));
            bool UserCheck = false;
            UserCheck = true;
            UserSettings.Ban_Count += 1;
            UserSettings.Followers = 0;
            DB.DBLists.UpdateServerRanks(new List<DB.ServerRanks> { UserSettings });
            if (!UserCheck)
            {
                DB.ServerRanks newEntry = new DB.ServerRanks
                {
                    Server_ID = e.Guild.Id.ToString(),
                    Ban_Count = 1,
                    Kick_Count = 0,
                    Warning_Level = 0,
                    User_ID = e.Member.Id.ToString()
                };
                DB.DBLists.InsertServerRanks(newEntry);
            }
            await Task.Delay(0);
        }

        private async Task Ban_Removed(GuildBanRemoveEventArgs e)
        {
            var wkb_Settings = (from ss in DB.DBLists.ServerSettings
                                where ss.ID_Server == e.Guild.Id.ToString()
                                select ss).ToList();
            DiscordGuild Guild = await Client.GetGuildAsync(Convert.ToUInt64(wkb_Settings[0].ID_Server));
            if (wkb_Settings[0].WKB_Log != "0")
            {
                await Task.Delay(1000);
                var logs = await Guild.GetAuditLogsAsync(1, action_type: AuditLogActionType.Ban);
                DiscordChannel wkbLog = Guild.GetChannel(Convert.ToUInt64(wkb_Settings[0].WKB_Log));
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder
                {
                    Title = $"✓ {e.Member.Username} ({e.Member.Id}) has been unbanned",
                    Description = $"*by {logs[0].UserResponsible.Mention}*",
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        IconUrl = e.Member.AvatarUrl,
                        Text = $"User unbanned"
                    },
                    Color = new DiscordColor(0x606060),
                };
                await wkbLog.SendMessageAsync(embed: embed);
            }
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "LiveBot", "Client is ready to process events.", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            var list = (from ss in DB.DBLists.ServerSettings
                        where ss.ID_Server == e.Guild.Id.ToString()
                        select ss).ToList();
            if (list.Count == 0)
            {
                string[] arr = new string[] { "0", "0", "0" };
                var newEntry = new DB.ServerSettings()
                {
                    ID_Server = e.Guild.Id.ToString(),
                    Delete_Log = "0",
                    User_Traffic = "0",
                    Welcome_Settings = arr,
                    WKB_Log = "0"
                };
                DB.DBLists.InsertServerSettings(newEntry);
            }
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "LiveBot", $"Guild available: {e.Guild.Name}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "LiveBot", $"Exception occurred: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "LiveBot", $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'", DateTime.Now);

            DB.DBLists.LoadCUC();
            string CommandName = e.Command.Name;
            var DBEntry = DB.DBLists.CommandsUsedCount.Where(w => w.Name == CommandName).FirstOrDefault();
            if (DBEntry == null)
            {
                DB.CommandsUsedCount NewEntry = new DB.CommandsUsedCount()
                {
                    Name = e.Command.Name,
                    Used_Count = 1
                };
                DB.DBLists.InsertCUC(NewEntry);
            }
            else if (DBEntry != null)
            {
                DBEntry.Used_Count++;
                DB.DBLists.UpdateCUC(new List<DB.CommandsUsedCount> { DBEntry });
            }
            return Task.CompletedTask;
        }

        private async Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Error, "LiveBot", $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}", DateTime.Now);
            if (e.Exception.InnerException != null)
            {
                e.Context.Client.DebugLogger.LogMessage(LogLevel.Error, "LiveBot", $"{e.Exception.InnerException.Message}", DateTime.Now);
            }
#pragma warning disable IDE0059 // Value assigned to symbol is never used
            if (e.Exception is ChecksFailedException ex)
#pragma warning restore IDE0059 // Value assigned to symbol is never used
            {
                foreach (var item in ex.FailedChecks)
                {
                    Console.WriteLine(item);
                }
                var no_entry = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");
                var clock = DiscordEmoji.FromName(e.Context.Client, ":clock:");
                string msgContent;
                if (ex.FailedChecks[0].GetType() == typeof(CooldownAttribute))
                {
                    msgContent = $"{clock} You, {e.Context.Member.Mention}, tried to execute the command too fast, wait and try again later.";
                }
                else if (ex.FailedChecks[0].GetType() == typeof(RequireRolesAttribute))
                {
                    msgContent = $"{no_entry} You, {e.Context.Member.Mention}, don't have the required role for this command";
                }
                else
                {
                    msgContent = $"{no_entry} You, {e.Context.Member.Mention}, do not have the permissions required to execute this command.";
                }
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = msgContent,
                    Color = new DiscordColor(0xFF0000) // red
                };
                DiscordMessage errorMSG = await e.Context.RespondAsync("", embed: embed);
                await Task.Delay(10000).ContinueWith(t => errorMSG.DeleteAsync());
            }
        }
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

    internal class ActivateRolesTimer
    {
        public DiscordGuild Guild { get; set; }
        public DiscordRole Role { get; set; }
        public DateTime Time { get; set; }
    }
    internal class GuildTimer
    {
        public DiscordGuild Guild { get; set; }
        public DateTime Time { get; set; }
    }
}