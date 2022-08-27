using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using DSharpPlus.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace LiveBot.SlashCommands
{
    internal class SlashCommands : ApplicationCommandModule
    {
        [SlashCommand("Livebot-info", "Information about live bot")]
        public async Task LiveBotInfo(InteractionContext ctx)
        {
            DateTime current = DateTime.UtcNow;
            TimeSpan time = current - Program.start;
            string changelog = "[REMOVED] Removed old leveling system.\n" +
                "[NEW] Added a time based activity tracking. Meaning you have to stay active to be at the top of the server ranks\n" +
                "[NEW] Various internal code changes and fixes\n" +
                "";
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
        [SlashCommand("Send-ModMail", "Creates a new ModMailChannel")]
        public async Task ModMail(InteractionContext ctx, [Option("subject", "Short Description of the issue")] string subject = "*Subject left blank*")
        {
            await ctx.DeferAsync(true);
            if (ctx.Guild == null)
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

            if (serverSettings.ModMailID == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The Mod Mail feature has not been set up in this server. Can't open ModMail."));
                return;
            }

            if (DB.DBLists.ModMail.Any(w => w.User_ID == ctx.User.Id && w.IsActive))
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

        [SlashRequireGuild]
        [SlashCommand("roletag", "Pings a role under specific criteria.")]
        public async Task RoleTag(InteractionContext ctx, [Autocomplete(typeof(RoleTagOptions))][Option("Role", "Which role to tag")] long id)
        {
            await ctx.DeferAsync(true);
            DB.RoleTagSettings roleTagSettings = DB.DBLists.RoleTagSettings.FirstOrDefault(w => w.ID == id);
            if (roleTagSettings == null || roleTagSettings.Server_ID != ctx.Guild.Id || roleTagSettings.Channel_ID != ctx.Channel.Id)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The role you tried to select does not exist or can't be tagged in this channel."));
                return;
            }
            if (roleTagSettings.Last_Used > DateTime.UtcNow - TimeSpan.FromMinutes(roleTagSettings.Cooldown))
            {
                TimeSpan remainingTime = TimeSpan.FromMinutes(roleTagSettings.Cooldown) - (DateTime.UtcNow - roleTagSettings.Last_Used);
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"This role can't be mentioned right now, cooldown has not passed yet. ({remainingTime.Hours} Hours {remainingTime.Minutes} Minutes {remainingTime.Seconds} Seconds left)"));
                return;
            }
            DiscordRole role = ctx.Guild.GetRole(roleTagSettings.Role_ID);

            await new DiscordMessageBuilder()
                            .WithContent($"{role.Mention} - {ctx.Member.Mention}: {roleTagSettings.Message}")
                            .WithAllowedMention(new RoleMention(role))
                            .SendAsync(ctx.Channel);

            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Role Tagged"));
            roleTagSettings.Last_Used = DateTime.UtcNow;
            DB.DBLists.UpdateRoleTagSettings(roleTagSettings);
        }

        private sealed class RoleTagOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                List<DiscordAutoCompleteChoice> result = new();
                foreach (var item in DB.DBLists.RoleTagSettings.Where(w => w.Server_ID == ctx.Guild.Id && w.Channel_ID == ctx.Channel.Id))
                {
                    result.Add(new DiscordAutoCompleteChoice($"{(item.Last_Used > DateTime.UtcNow - TimeSpan.FromMinutes(item.Cooldown) ? "(On cooldown) " : "")}{item.Description}", item.ID));
                }

                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        [SlashRequireGuild]
        [SlashCommand("Leaderboard", "Current server leaderboard.")]
        public async Task Leaderboard(InteractionContext ctx, [Option("Page","A page holds 10 entries.")][Minimum(1)] long page)
        {
            await ctx.DeferAsync();
            var ActivityList = DB.DBLists.UserActivity
                .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.Guild_ID == ctx.Guild.Id)
                .GroupBy(w => w.User_ID, w => w.Points, (key, g) => new { UserID = key, Points = g.ToList() })
                .OrderByDescending(w => w.Points.Sum())
                .ToList();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("```csharp\n📋 Rank | Username");
            for (int i = ((int)page * 10) - 10; i < (int)page * 10; i++)
            {
                DiscordUser user = await ctx.Client.GetUserAsync(ActivityList[i].UserID);
                stringBuilder.AppendLine($"[{i + 1}]\t# {user.Username}\n\t\t\tPoints:{ActivityList[i].Points.Sum()}");
                if (i == ActivityList.Count - 1)
                {
                    i = (int)page * 10;
                }
            }
            int rank = 0;
            string personalscore = string.Empty;
            foreach (var item in ActivityList)
            {
                rank++;
                if (item.UserID==ctx.User.Id)
                {
                    personalscore = $"⭐Rank: {rank}\t Points: {item.Points.Sum()}";
                }
            }
            stringBuilder.AppendLine($"\n# Your Ranking\n{personalscore}\n```");

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(stringBuilder.ToString()));
        }

    }
}