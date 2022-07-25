using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace LiveBot.SlashCommands
{
    internal class SlashCommands : ApplicationCommandModule
    {
        [SlashCommand("Livebot-info","Information about live bot")]
        public async Task LiveBotInfo(InteractionContext ctx)
        {
            DateTime current = DateTime.UtcNow;
            TimeSpan time = current - Program.start;
            string changelog = "[NEW] Mod Mail initialised with slash commands in server now\n" +
                "[NEW] Moderators can use slash command to respond to a mod mail. It provides ID's of open MM's\n" +
                "[NEW] Users can now close the mod mail via a button in their DMs.\n" +
                "[NEW] My-Summit command will only show platforms that you have linked. You can still not specify any and it will default to one, but if you need to chose you can select.\n" +
                "[NEW] You can now unlink your hub account from discord.\n" +
                "[FIX] Bunch of back end fixes and improvements for the bot\n" +
                "[NEW] Slash command bot DM now adds a button to open a mod mail directly.\n" +
                "[FIX] Delete log searching for incorrect file fixed\n" +
                "[CHANGE] Mod mail blocklist commands moved from `/mod` to `/modmail` group and renamed to `blacklist` and `unlist`";
            DiscordUser user = ctx.Client.CurrentUser;
            var embed = new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = user.AvatarUrl,
                    Name = user.Username
                }
            };
            embed.AddField("Version:", Program.BotVersion, true);
            embed.AddField("Uptime:", $"{time.Days} Days {time.Hours}:{time.Minutes}.{time.Seconds}", true);

            embed.AddField("Programmed in:", "C#", true);
            embed.AddField("Programmed by:", "<@86725763428028416>", true);
            embed.AddField("LiveBot info", "General purpose bot with a level system, stream notifications, greeting people and various other functions related to The Crew franchise");
            embed.AddField("Change log:", changelog);
            await ctx.CreateResponseAsync(embed: embed);
        }

        [SlashRequireGuild]
        [SlashCommand("Send-ModMail","Creates a new ModMailChannel")]
        public async Task ModMail(InteractionContext ctx, [Option("subject","Short Description of the issue")] string subject = "*Subject left blank*")
        {
            await ctx.DeferAsync(true);
            if (ctx.Guild==null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This command requires to be exectued in the server you wish to contact."));
                return;
            }

            if (DB.DBLists.ServerRanks.FirstOrDefault(w => w.Server_ID == ctx.Guild.Id && w.User_ID == ctx.User.Id).MM_Blocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You are blocked from using the Mod Mail feature in this server."));
                return;
            }
            DB.ServerSettings serverSettings = DB.DBLists.ServerSettings.FirstOrDefault(w => w.ID_Server == ctx.Guild.Id);

            if (serverSettings.ModMailID==0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The Mod Mail feature has not been set up in this server. Can't open ModMail."));
                return;
            }

            if (DB.DBLists.ModMail.Any(w=>w.User_ID==ctx.User.Id && w.IsActive))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You already have an existing Mod Mail open, please close it before starting a new one."));
                return;
            }


            Random r = new();
            string colorID = string.Format("#{0:X6}", r.Next(0x1000000));
            DB.ModMail newEntry = new()
            {
                Server_ID = ctx.Guild.Id,
                User_ID = ctx.User.Id,
                LastMSGTime = DateTime.UtcNow,
                ColorHex = colorID,
                IsActive = true,
                HasChatted = false
            };

            long EntryID = DB.DBLists.InsertModMailGetID(newEntry);
            DiscordButtonComponent CloseButton = new(ButtonStyle.Danger, $"close{EntryID}", "Close", false, new DiscordComponentEmoji("✖️"));
            
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Mod Mail #{EntryID} opened, please head over to your Direct Messages with Live Bot to chat to the moderator team!"));


            await ctx.Member.SendMessageAsync(new DiscordMessageBuilder().AddComponents(CloseButton).WithContent($"**----------------------------------------------------**\n" +
                            $"Modmail entry **open** with `{ctx.Guild.Name}`. Continue to write as you would normally ;)\n*Mod Mail will time out in {Automation.ModMail.TimeoutMinutes} minutes after last message is sent.*\n" +
                            $"**Subject: {subject}**"));

            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{ctx.User.Username} ({ctx.User.Id})",
                    IconUrl = ctx.User.AvatarUrl
                },
                Title = $"[NEW] #{EntryID} Mod Mail created by {ctx.User.Username}.",
                Color = new DiscordColor(colorID),
                Description = subject
            };

            DiscordChannel modMailChannel = ctx.Guild.GetChannel(serverSettings.ModMailID);
            await new DiscordMessageBuilder()
                .AddComponents(CloseButton)
                .WithEmbed(embed)
                .SendAsync(modMailChannel);

        }
    }
}
