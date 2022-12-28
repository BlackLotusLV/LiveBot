using System.Reflection.Metadata;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.EventArgs;
using LiveBot.Automation;
using LiveBot.Json;
using LiveBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SixLabors.Fonts;

namespace LiveBot
{
    internal sealed class Program
    {
        public static DiscordClient Client { get; private set; }
        public SlashCommandsExtension Slash { get; private set; }
        public CommandsNextExtension Commands { get; private set; }
        public static readonly DateTime Start = DateTime.UtcNow;
        public const string BotVersion = $"20221228_B";
        public static bool TestBuild { get; set; } = true;
        // TC Hub

        public static ConfigJson.TheCrewHubApi TheCrewHubJson { get; set; }
        public static TCHubJson.TCHub TheCrewHub { get; set; }
        public static List<TCHubJson.Summit> JSummit { get; set; }
        public static ConfigJson.Bot ConfigJson { get; set; }

        // Lists

        public static List<ulong> ServerIdList { get; set; } = new();

        // string

        public static readonly string TmpLoc = $"{Path.GetTempPath()}/livebot-";

        // fonts
        public static FontCollection Fonts { get; set; } = new();

        // Timers
        private Timer MessageCacheClearTimer { get; set; } = new(e => AutoMod.ClearMSGCache());
        private Timer ModMailCloserTimer { get; set; } = new(async e => await ModMail.ModMailCloser());
        private Timer HubUpdateTimer { get; set; } = new(async e => await HubMethods.UpdateHubInfo());

        private static void Main(string[] args)
        {
            Program prog = new Program();
            prog.RunBotAsync(args).GetAwaiter().GetResult();
        }

        public async Task RunBotAsync(string[] args)
        {
            // Load Fonts
            Fonts.Add("Assets/Fonts/HurmeGeometricSans4-Black.ttf");
            Fonts.Add("Assets/Fonts/Noto Sans Mono CJK JP Bold.otf");
            Fonts.Add("Assets/Fonts/NotoSansArabic-Bold.ttf");
            // Load Config
            string json;
            using (StreamReader sr = new(File.OpenRead("Config.json"), new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();
            ConfigJson = JsonConvert.DeserializeObject<ConfigJson.Config>(json).DevBot;

            // Start The Crew Hub service
            TheCrewHubJson = JsonConvert.DeserializeObject<ConfigJson.Config>(json).TCHub;
            Thread hubThread = new(async () => await HubMethods.UpdateHubInfo());
            hubThread.Start();
            
            LogLevel logLevel = LogLevel.Debug;
            if (args.Length == 1 && args[0] == "live") // Checks for command argument to be "live", if so, then launches the live version of the bot, not dev
            {
                ConfigJson = JsonConvert.DeserializeObject<ConfigJson.Config>(json).LiveBot;

                TestBuild = false;
                logLevel = LogLevel.Information;
            }
            DiscordConfiguration cfg = new()
            {
                Token = ConfigJson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                ReconnectIndefinitely = false,
                MinimumLogLevel = logLevel,
                Intents = DiscordIntents.All,
                LogUnknownEvents = false
            };
            Client = new DiscordClient(cfg);

            ServiceProvider service = new ServiceCollection()
                .AddSingleton<IWarningService, WarningService>()
                .AddSingleton<IStreamNotificationService, StreamNotificationService>()
                .AddSingleton<ILeaderboardService, LeaderboardService>()
                .BuildServiceProvider();
            
            DB.DBLists.LoadAllLists(); // loads data from database
            Client.Ready += Client_Ready;
            Client.GuildAvailable += this.Client_GuildAvailable;
            Client.ClientErrored += this.Client_ClientError;

            Client.UseInteractivity(new InteractivityConfiguration
            {
                PaginationBehaviour = DSharpPlus.Interactivity.Enums.PaginationBehaviour.Ignore,
                Timeout = TimeSpan.FromMinutes(2)
            });
            CommandsNextConfiguration commandNextConfig = new()
            {
                StringPrefixes = new string[] { ConfigJson.CommandPrefix },
                CaseSensitive = false,
                IgnoreExtraArguments = true,
                Services = service
            };
            SlashCommandsConfiguration slashCommandConfig = new()
            {
                Services = service
            };

            this.Slash = Client.UseSlashCommands(slashCommandConfig);
            this.Commands = Client.UseCommandsNext(commandNextConfig);

            this.Commands.CommandExecuted += this.Commands_CommandExecuted;
            this.Commands.CommandErrored += this.Commands_CommandErrored;

            this.Slash.SlashCommandExecuted += this.Slash_Commands_CommandExecuted;
            this.Slash.SlashCommandErrored += this.Slash_Commands_CommandErrored;
            this.Slash.ContextMenuExecuted += this.Context_Menu_Executed;
            this.Slash.ContextMenuErrored += this.Context_Menu_Errored;

            this.Commands.RegisterCommands<Commands.UngroupedCommands>();
            this.Commands.RegisterCommands<Commands.AdminCommands>();
            this.Commands.RegisterCommands<Commands.OCommands>();
            this.Commands.RegisterCommands<Commands.ModMailCommands>();

            //*/

            // Services
            IWarningService warningService = service.GetService<IWarningService>();
            IStreamNotificationService streamNotificationService = service.GetService<IStreamNotificationService>();
            ILeaderboardService leaderboardService = service.GetService<ILeaderboardService>();
            
            warningService.StartService(Client);
            streamNotificationService.StartService(Client);
            leaderboardService.StartService(Client);

            AutoMod autoMod = ActivatorUtilities.CreateInstance<AutoMod>(service);
            LiveStream liveStream = ActivatorUtilities.CreateInstance<LiveStream>(service);
            UserActivityTracker userActivityTracker = ActivatorUtilities.CreateInstance<UserActivityTracker>(service);
            
            

            //

            if (!TestBuild) //Only enables these when using live version
            {
                Client.Logger.LogInformation("Running liver version: {version}", BotVersion);
                Client.PresenceUpdated += liveStream.Stream_Notification;

                Client.GuildMemberAdded += autoMod.Add_To_Leaderboards;
                Client.MessageCreated += AutoMod.Media_Only_Filter;
                Client.MessageCreated += autoMod.Banned_Words;
                Client.MessageCreated += autoMod.Spam_Protection;
                Client.MessageCreated += autoMod.Link_Spam_Protection;
                Client.MessageCreated += autoMod.Everyone_Tag_Protection;
                Client.MessageDeleted += AutoMod.Delete_Log;
                Client.MessagesBulkDeleted += AutoMod.Bulk_Delete_Log;
                Client.GuildMemberAdded += AutoMod.User_Join_Log;
                Client.GuildMemberRemoved += AutoMod.User_Leave_Log;
                Client.GuildMemberRemoved += AutoMod.User_Kicked_Log;
                Client.GuildBanAdded += AutoMod.User_Banned_Log;
                Client.GuildBanRemoved += AutoMod.User_Unbanned_Log;
                Client.VoiceStateUpdated += AutoMod.Voice_Activity_Log;
                Client.GuildMemberUpdated += AutoMod.User_Timed_Out_Log;

                Client.MessageCreated += userActivityTracker.Add_Points;

                Client.ComponentInteractionCreated += Roles.Button_Roles;

                Client.GuildMemberAdded += MemberFlow.Welcome_Member;
                Client.GuildMemberRemoved += MemberFlow.Say_Goodbye;

                Client.GuildMemberUpdated += MembershipScreening.AcceptRules;

                Client.MessageCreated += ModMail.ModMailDM;
                Client.ComponentInteractionCreated += ModMail.ModMailCloseButton;
                Client.ComponentInteractionCreated += ModMail.ModMailDMOpenButton;

                this.Slash.RegisterCommands<SlashCommands.SlashTheCrewHubCommands>(150283740172517376);
                this.Slash.RegisterCommands<SlashCommands.SlashModeratorCommands>();
                this.Slash.RegisterCommands<SlashCommands.SlashCommands>();
                this.Slash.RegisterCommands<SlashCommands.SlashModMailCommands>();
            }
            else
            {
                Client.Logger.LogInformation("Running in test build mode");
                this.Slash.RegisterCommands<SlashCommands.SlashTheCrewHubCommands>(282478449539678210);
                this.Slash.RegisterCommands<SlashCommands.SlashModeratorCommands>(282478449539678210);
                this.Slash.RegisterCommands<SlashCommands.SlashAdministratorCommands>(282478449539678210);
                this.Slash.RegisterCommands<SlashCommands.SlashCommands>(282478449539678210);
                this.Slash.RegisterCommands<SlashCommands.SlashModMailCommands>(282478449539678210);

                Client.ScheduledGuildEventCreated += GuildEvents.Event_Created;
            }
            DiscordActivity botActivity = new($"/send-modmail to open a chat with moderators", ActivityType.Playing);
            await Client.ConnectAsync(botActivity);
            await Task.Delay(-1);
        }

        private static Task Client_Ready(DiscordClient client, ReadyEventArgs e)
        {
            client.Logger.LogInformation(CustomLogEvents.LiveBot, "[LiveBot] Client is ready to process events.");
            return Task.CompletedTask;
        }

        private Task Client_GuildAvailable(DiscordClient client, GuildCreateEventArgs e)
        {
            ServerIdList.Add(e.Guild.Id);
            var list = (from ss in DB.DBLists.ServerSettings
                        where ss.ID_Server == e.Guild.Id
                        select ss).ToList();
            if (list.Count == 0)
            {
                var newEntry = new DB.ServerSettings()
                {
                    ID_Server = e.Guild.Id,
                    Delete_Log = 0,
                    User_Traffic = 0,
                    WKB_Log = 0,
                    Spam_Exception_Channels = new ulong[] { 0 }
                };
                DB.DBLists.InsertServerSettings(newEntry);
            }
            if (e.Guild.Id == 150283740172517376)
            {
                HubUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(30));
                MessageCacheClearTimer.Change(TimeSpan.Zero, TimeSpan.FromDays(1));
                ModMailCloserTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
            }
            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Guild available: {GuildName}", e.Guild.Name);
            return Task.CompletedTask;
        }

        private Task Client_ClientError(DiscordClient client, ClientErrorEventArgs e)
        {
            client.Logger.LogError(CustomLogEvents.ClientError, e.Exception, "Exception occurred");
            return Task.CompletedTask;
        }

        private Task Commands_CommandExecuted(CommandsNextExtension ext, CommandExecutionEventArgs e)
        {
            Client.Logger.LogInformation(CustomLogEvents.CommandExecuted, "{Username} successfully executed '{CommandName}' command", e.Context.User.Username, e.Command.QualifiedName);
            return Task.CompletedTask;
        }

        private async Task Commands_CommandErrored(CommandsNextExtension ext, CommandErrorEventArgs e)
        {
            Client.Logger.LogError(CustomLogEvents.CommandError, e.Exception, "{Username} tried executing '{CommandName}' but it errored", e.Context.User.Username, e.Command?.QualifiedName ?? "<unknown command>");
            if (e.Exception is ChecksFailedException ex)
            {
                var noEntry = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");
                string msgContent;
                if (ex.FailedChecks[0] is CooldownAttribute)
                {
                    msgContent = $"{DiscordEmoji.FromName(e.Context.Client, ":clock:")} You, {e.Context.Member.Mention}, tried to execute the command too fast, wait and try again later.";
                }
                else if (ex.FailedChecks[0] is RequireRolesAttribute)
                {
                    msgContent = $"{noEntry} You, {e.Context.User.Mention}, don't have the required role for this command";
                }
                else if (ex.FailedChecks[0] is RequireDirectMessageAttribute)
                {
                    msgContent = $"{noEntry} You are trying to use a command that is only available in DMs";
                }
                else
                {
                    msgContent = $"{noEntry} You, {e.Context.User.Mention}, do not have the permissions required to execute this command.";
                }
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = msgContent,
                    Color = new DiscordColor(0xFF0000) // red
                };
                DiscordMessage errorMsg = await e.Context.RespondAsync(string.Empty, embed: embed);
                await Task.Delay(10000).ContinueWith(t => errorMsg.DeleteAsync());
            }
        }

        private Task Slash_Commands_CommandExecuted(SlashCommandsExtension ext, SlashCommandExecutedEventArgs e)
        {
            Client.Logger.LogInformation(CustomLogEvents.SlashExecuted, "{Username} successfully executed '{CommandName}' command", e.Context.User.Username, e.Context.CommandName);
            return Task.CompletedTask;
        }

        private Task Slash_Commands_CommandErrored(SlashCommandsExtension ext, SlashCommandErrorEventArgs e)
        {
            Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}' but it errored", e.Context.User.Username, e.Context.CommandName ?? "<unknown command>");
            return Task.CompletedTask;
        }

        private Task Context_Menu_Executed(SlashCommandsExtension ext, ContextMenuExecutedEventArgs e)
        {
            Client.Logger.LogInformation(CustomLogEvents.ContextMenuExecuted, "{Username} Successfully executed '{CommandName}' command", e.Context.User.Username, e.Context.CommandName);
            return Task.CompletedTask;
        }

        private Task Context_Menu_Errored(SlashCommandsExtension ext, ContextMenuErrorEventArgs e)
        {
            Client.Logger.LogError(CustomLogEvents.SlashErrored, e.Exception, "{Username} tried executing '{CommandName}' but it errored", e.Context.User.Username, e.Context.CommandName ?? "<unknown command>");
            return Task.CompletedTask;
        }
    }
}