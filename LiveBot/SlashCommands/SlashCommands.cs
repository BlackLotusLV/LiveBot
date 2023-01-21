using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using LiveBot.DB;

namespace LiveBot.SlashCommands
{
    internal class SlashCommands : ApplicationCommandModule
    {
        [SlashCommand("LiveBot-info", "Information about live bot")]
        public async Task LiveBotInfo(InteractionContext ctx)
        {
            DateTime current = DateTime.UtcNow;
            TimeSpan time = current - Program.Start;
            const string changelog = "[FIX] Summit leaderboard text formatter fix";
            DiscordUser user = ctx.Client.CurrentUser;
            DiscordEmbedBuilder embed = new()
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

        [SlashCommand("Send-ModMail", "Creates a new ModMailChannel")]
        public async Task ModMail(InteractionContext ctx, [Option("subject", "Short Description of the issue")] string subject = "*Subject left blank*")
        {
            await ctx.DeferAsync(true);
            if (ctx.Guild == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This command requires to be exectued in the server you wish to contact."));
                return;
            }
            ServerRanks userRanks = DBLists.ServerRanks.FirstOrDefault(w => w.GuildId == ctx.Guild.Id && w.UserDiscordId == ctx.User.Id);
            if (userRanks == null || userRanks.IsModMailBlocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You are blocked from using the Mod Mail feature in this server."));
                return;
            }
            ServerSettings serverSettings = DBLists.ServerSettings.FirstOrDefault(w => w.GuildId == ctx.Guild.Id);

            if (serverSettings?.ModMailChannelId == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The Mod Mail feature has not been set up in this server. Can't open ModMail."));
                return;
            }

            if (DBLists.ModMail.Any(w => w.UserDiscordId == ctx.User.Id && w.IsActive))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You already have an existing Mod Mail open, please close it before starting a new one."));
                return;
            }

            Random r = new();
            string colorId = $"#{r.Next(0x1000000):X6}";
            ModMail newEntry = new()
            {
                GuildId = ctx.Guild.Id,
                UserDiscordId = ctx.User.Id,
                LastMessageTime = DateTime.UtcNow,
                ColorHex = colorId,
                IsActive = true,
                HasChatted = false
            };

            long entryId = DB.DBLists.InsertModMail(newEntry);
            DiscordButtonComponent closeButton = new(ButtonStyle.Danger, $"close{entryId}", "Close", false, new DiscordComponentEmoji("✖️"));

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Mod Mail #{entryId} opened, please head over to your Direct Messages with Live Bot to chat to the moderator team!"));

            await ctx.Member.SendMessageAsync(new DiscordMessageBuilder().AddComponents(closeButton).WithContent($"**----------------------------------------------------**\n" +
                            $"Mod mail entry **open** with `{ctx.Guild.Name}`. Continue to write as you would normally ;)\n*Mod Mail will time out in {Automation.ModMail.TimeoutMinutes} minutes after last message is sent.*\n" +
                            $"**Subject: {subject}**"));

            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{ctx.User.Username} ({ctx.User.Id})",
                    IconUrl = ctx.User.AvatarUrl
                },
                Title = $"[NEW] #{entryId} Mod Mail created by {ctx.User.Username}.",
                Color = new DiscordColor(colorId),
                Description = subject
            };

            DiscordChannel modMailChannel = ctx.Guild.GetChannel(serverSettings.ModMailChannelId);
            await new DiscordMessageBuilder()
                .AddComponents(closeButton)
                .WithEmbed(embed)
                .SendAsync(modMailChannel);
        }

        [SlashRequireGuild]
        [SlashCommand("roletag", "Pings a role under specific criteria.")]
        public async Task RoleTag(InteractionContext ctx, [Autocomplete(typeof(RoleTagOptions))][Option("Role", "Which role to tag")] long id)
        {
            await ctx.DeferAsync(true);
            DB.RoleTagSettings roleTagSettings = DB.DBLists.RoleTagSettings.FirstOrDefault(w => w.Id == id);
            if (roleTagSettings == null || roleTagSettings.GuildId != ctx.Guild.Id || roleTagSettings.ChannelId != ctx.Channel.Id)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The role you tried to select does not exist or can't be tagged in this channel."));
                return;
            }
            if (roleTagSettings.LastTimeUsed > DateTime.UtcNow - TimeSpan.FromMinutes(roleTagSettings.Cooldown))
            {
                TimeSpan remainingTime = TimeSpan.FromMinutes(roleTagSettings.Cooldown) - (DateTime.UtcNow - roleTagSettings.LastTimeUsed);
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"This role can't be mentioned right now, cooldown has not passed yet. ({remainingTime.Hours} Hours {remainingTime.Minutes} Minutes {remainingTime.Seconds} Seconds left)"));
                return;
            }
            DiscordRole role = ctx.Guild.GetRole(roleTagSettings.RoleId);

            await new DiscordMessageBuilder()
                            .WithContent($"{role.Mention} - {ctx.Member.Mention}: {roleTagSettings.Message}")
                            .WithAllowedMention(new RoleMention(role))
                            .SendAsync(ctx.Channel);

            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Role Tagged"));
            roleTagSettings.LastTimeUsed = DateTime.UtcNow;
            DB.DBLists.UpdateRoleTagSettings(roleTagSettings);
        }

        private sealed class RoleTagOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                List<DiscordAutoCompleteChoice> result = new();
                foreach (var item in DB.DBLists.RoleTagSettings.Where(w => w.GuildId == ctx.Guild.Id && w.ChannelId == ctx.Channel.Id))
                {
                    result.Add(new DiscordAutoCompleteChoice($"{(item.LastTimeUsed > DateTime.UtcNow - TimeSpan.FromMinutes(item.Cooldown) ? "(On cooldown) " : "")}{item.Description}", item.Id));
                }

                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        [SlashRequireGuild]
        [SlashCommand("Rank","Shows your server rank without the leaderboard.")]
        public async Task Rank(InteractionContext ctx)
        {
            await ctx.DeferAsync();
            var ActivityList = DBLists.UserActivity
                .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.GuildId == ctx.Guild.Id)
                .GroupBy(w => w.UserDiscordId, w => w.Points, (key, g) => new { UserID = key, Points = g.ToList() })
                .OrderByDescending(w => w.Points.Sum())
                .ToList();
            var userInfo = DBLists.Leaderboard.FirstOrDefault(w => w.UserDiscordId == ctx.User.Id);
            if (userInfo == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find your rank in the databse"));
                return;
            }
            int rank = 0;
            foreach (var item in ActivityList)
            {
                rank++;
                if (item.UserID==ctx.User.Id)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You are ranked **#{rank}** in {ctx.Guild.Name} server with **{item.Points.Sum()}** points. Your cookie stats are: {userInfo.CookiesTaken} Received /  {userInfo.CookiesGiven} Given"));
                    break;
                }
            }
        }

        [SlashRequireGuild]
        [SlashCommand("Leaderboard", "Current server leaderboard.")]
        public async Task Leaderboard(InteractionContext ctx, [Option("Page","A page holds 10 entries.")][Minimum(1)] long page = 1)
        {
            await ctx.DeferAsync();

            List<DiscordButtonComponent> buttons = new()
            {
                new DiscordButtonComponent(ButtonStyle.Primary, "left", "",false,new DiscordComponentEmoji("◀️")),
                new DiscordButtonComponent(ButtonStyle.Danger,"end","",false,new DiscordComponentEmoji("⏹")),
                new DiscordButtonComponent(ButtonStyle.Primary, "right", "",false,new DiscordComponentEmoji("▶️"))
            };

            DiscordMessage message = await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(await GenerateLeaderboardAsync(ctx,(int)page)).AddComponents(buttons));

            bool end = false;
            do
            {
                var result = await message.WaitForButtonAsync(ctx.User, TimeSpan.FromSeconds(30));
                if (result.TimedOut)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(message.Content));
                    return;
                }
                switch (result.Result.Id)
                {
                    case "left":
                        if (page > 1)
                        {
                            page--;
                            await message.ModifyAsync(await GenerateLeaderboardAsync(ctx, (int)page));
                        }
                        break;

                    case "right":
                        page++;
                        try
                        {
                            await message.ModifyAsync(await GenerateLeaderboardAsync(ctx, (int)page));
                        }
                        catch (Exception)
                        {
                            page--;
                        }
                        break;
                    case "end":
                        end = true;
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(message.Content));
                        break;
                }
                await result.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
            } while (!end);
        }

        private static async Task<string> GenerateLeaderboardAsync(InteractionContext ctx, int page)
        {
            var ActivityList = DBLists.UserActivity
                .Where(w => w.Date > DateTime.UtcNow.AddDays(-30) && w.GuildId == ctx.Guild.Id)
                .GroupBy(w => w.UserDiscordId, w => w.Points, (key, g) => new { UserID = key, Points = g.ToList() })
                .OrderByDescending(w => w.Points.Sum())
                .ToList();
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("```csharp\n📋 Rank | Username");
            for (int i = (page * 10) - 10; i < page * 10; i++)
            {
                DiscordUser user = await ctx.Client.GetUserAsync(ActivityList[i].UserID);
                Leaderboard userInfo = DBLists.Leaderboard.FirstOrDefault(w => w.UserDiscordId == user.Id);
                stringBuilder.Append($"[{i + 1}]\t# {user.Username}\n\t\t\tPoints:{ActivityList[i].Points.Sum()}");
                if (userInfo!=null)
                {
                    stringBuilder.AppendLine($"\t\t🍪:{userInfo.CookiesTaken}/{userInfo.CookiesGiven}");
                }
                if (i == ActivityList.Count - 1)
                {
                    i = page * 10;
                }
            }
            int rank = 0;
            StringBuilder personalScore = new();
            foreach (var item in ActivityList)
            {
                rank++;
                if (item.UserID == ctx.User.Id)
                {
                    Leaderboard userInfo = DBLists.Leaderboard.FirstOrDefault(w => w.UserDiscordId == ctx.User.Id);
                    personalScore.Append($"⭐Rank: {rank}\t Points: {item.Points.Sum()}");
                    if (userInfo!=null)
                    {
                        personalScore.AppendLine($"\t🍪:{userInfo.CookiesTaken}/{userInfo.CookiesGiven}");
                    }
                }
            }
            stringBuilder.AppendLine($"\n# Your Ranking\n{personalScore.ToString()}\n```");
            return stringBuilder.ToString();
        }

        [SlashRequireGuild]
        [SlashCommand("cookie", "Gives a user a cookie.")]
        public async Task Cookie(InteractionContext ctx, [Option("User", "Who to give the cooky to")] DiscordUser member)
        {
            await ctx.DeferAsync(true);
            if (ctx.Member == member)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You can't give yourself a cookie"));
                return;
            }
            Leaderboard giver = DBLists.Leaderboard.FirstOrDefault(f => f.UserDiscordId == ctx.Member.Id);
            Leaderboard receiver = DBLists.Leaderboard.FirstOrDefault(f => f.UserDiscordId == member.Id);

            if (giver == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You do not have a Database entry, one will be created, please try again."));
                
            }
            if (giver?.CookieDate.Date == DateTime.UtcNow.Date)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Your cookie box is empty. You can give a cookie in {24-DateTime.UtcNow.Hour} Hours, {(59-DateTime.UtcNow.Minute)-1} Minutes, {(59-DateTime.UtcNow.Second)} Seconds."));
                return;
            }

            giver.CookieDate = DateTime.UtcNow.Date;
            giver.CookiesGiven++;
            receiver.CookiesTaken++;
            DBLists.UpdateLeaderboard(giver);
            DBLists.UpdateLeaderboard(receiver);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Cookie given."));

            await new DiscordMessageBuilder()
                .WithContent($"{member.Mention}, {ctx.Member.Username} has given you a :cookie:")
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }
    }
}