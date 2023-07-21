using System.Collections.Immutable;
using System.Net.Http;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiveBot.SlashCommands
{
    internal sealed class SlashCommands : ApplicationCommandModule
    {
        public IModMailService ModMailService { private get; set; }
        public LiveBotDbContext DatabaseContext { private get; set; }
        public IDatabaseMethodService DatabaseMethodService { private get; set; }
        
        [SlashCommand("LiveBot-info", "Information about live bot")]
        public async Task LiveBotInfo(InteractionContext ctx)
        {
            const string changelog = "- Adjusted formatting for mod logs\n" +
                                     "- Mod logs now hook in to AuditLogs for accurate and more data";
            DiscordUser user = ctx.Client.CurrentUser;
            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = user.AvatarUrl,
                    Name = user.Username
                }
            };
            embed.AddField("Version:", "test", true);

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
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This command requires to be executed in the server you wish to contact."));
                return;
            }
            GuildUser userRanks = await DatabaseContext.GuildUsers.FirstOrDefaultAsync(w => w.GuildId == ctx.Guild.Id && w.UserDiscordId == ctx.User.Id);
            if (userRanks == null || userRanks.IsModMailBlocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You are blocked from using the Mod Mail feature in this server."));
                return;
            }
            Guild guild = await DatabaseContext.Guilds.FirstOrDefaultAsync(w => w.Id == ctx.Guild.Id);

            if (guild == null || guild.ModMailChannelId == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The Mod Mail feature has not been set up in this server. Can't open ModMail."));
                return;
            }

            if (await DatabaseContext.ModMail.AnyAsync(w => w.UserDiscordId == ctx.User.Id && w.IsActive))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You already have an existing Mod Mail open, please close it before starting a new one."));
                return;
            }

            Random r = new();
            var colorId = $"#{r.Next(0x1000000):X6}";
            ModMail newEntry = new(ctx.Guild.Id,ctx.User.Id,DateTime.UtcNow,colorId);
            await DatabaseMethodService.AddModMailAsync(newEntry);

            DiscordButtonComponent closeButton = new(ButtonStyle.Danger, $"{ModMailService.CloseButtonPrefix}{newEntry.Id}", "Close", false, new DiscordComponentEmoji("✖️"));

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Mod Mail #{newEntry.Id} opened, please head over to your Direct Messages with Live Bot to chat to the moderator team!"));

            await ctx.Member.SendMessageAsync(new DiscordMessageBuilder().AddComponents(closeButton).WithContent($"**----------------------------------------------------**\n" +
                            $"Mod mail entry **open** with `{ctx.Guild.Name}`. Continue to write as you would normally ;)\n*Mod Mail will time out in {ModMailService.TimeoutMinutes} minutes after last message is sent.*\n" +
                            $"**Subject: {subject}**"));

            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{ctx.User.Username} ({ctx.User.Id})",
                    IconUrl = ctx.User.AvatarUrl
                },
                Title = $"[NEW] #{newEntry.Id} Mod Mail created by {ctx.User.Username}.",
                Color = new DiscordColor(colorId),
                Description = subject
            };

            DiscordChannel modMailChannel = ctx.Guild.GetChannel(guild.ModMailChannelId.Value);
            await new DiscordMessageBuilder()
                .AddComponents(closeButton)
                .WithEmbed(embed)
                .SendAsync(modMailChannel);
        }

        [SlashRequireGuild]
        [SlashCommand("RoleTag", "Pings a role under specific criteria.")]
        public async Task RoleTag(InteractionContext ctx, [Autocomplete(typeof(RoleTagOptions))][Option("Role", "Which role to tag")] long id)
        {
            await ctx.DeferAsync(true);
            RoleTagSettings roleTagSettings = await DatabaseContext.RoleTagSettings.FindAsync(id);
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
            
            DatabaseContext.RoleTagSettings.Update(roleTagSettings);
            await DatabaseContext.SaveChangesAsync();
        }

        private sealed class RoleTagOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                List<DiscordAutoCompleteChoice> result = new();
                foreach (RoleTagSettings item in databaseContext.RoleTagSettings.Where(w => w.GuildId == ctx.Guild.Id && w.ChannelId == ctx.Channel.Id))
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
            var activityList = await DatabaseContext.UserActivity
                .Where(x => x.Date > DateTime.UtcNow.AddDays(-30) && x.GuildId == ctx.Guild.Id)
                .GroupBy(x => x.UserDiscordId)
                .Select(g => new { UserID = g.Key, Points = g.Sum(x => x.Points) })
                .OrderByDescending(x => x.Points)
                .ToListAsync();
            User userInfo = await DatabaseContext.Users.FindAsync(ctx.User.Id);
            if (userInfo == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find your rank in the database"));
                return;
            }
            var rank = 0;
            foreach (var item in activityList)
            {
                rank++;
                if (item.UserID != ctx.User.Id) continue;
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You are ranked **#{rank}** in {ctx.Guild.Name} server with **{item.Points}** points. Your cookie stats are: {userInfo.CookiesTaken} Received /  {userInfo.CookiesGiven} Given"));
                break;
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

            string board = await GenerateLeaderboardAsync(ctx, (int)page);
            DiscordMessage message = await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(board).AddComponents(buttons));

            var end = false;
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
                            board = await GenerateLeaderboardAsync(ctx, (int)page);
                            await message.ModifyAsync(board);
                        }
                        break;

                    case "right":
                        page++;
                        try
                        {
                            board = await GenerateLeaderboardAsync(ctx, (int)page);
                            await message.ModifyAsync(board);
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

        private async Task<string> GenerateLeaderboardAsync(InteractionContext ctx, int page)
        {
            var activityList = await DatabaseContext.UserActivity
                .Where(x => x.Date > DateTime.UtcNow.AddDays(-30) && x.GuildId == ctx.Guild.Id)
                .GroupBy(x => x.UserDiscordId)
                .Select(g => new { UserID = g.Key, Points = g.Sum(x => x.Points) })
                .OrderByDescending(x => x.Points)
                .ToListAsync();
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("```csharp\n📋 Rank | Username");
            for (int i = (page * 10) - 10; i < page * 10; i++)
            {
                DiscordUser user = await ctx.Client.GetUserAsync(activityList[i].UserID);
                User userInfo = await DatabaseContext.Users.FindAsync(user.Id);
                stringBuilder.Append($"[{i + 1}]\t# {user.Username}\n\t\t\tPoints:{activityList[i].Points}");
                if (userInfo!=null)
                {
                    stringBuilder.AppendLine($"\t\t🍪:{userInfo.CookiesTaken}/{userInfo.CookiesGiven}");
                }
                if (i == activityList.Count - 1)
                {
                    i = page * 10;
                }
            }
            var rank = 0;
            StringBuilder personalScore = new();
            foreach (var item in activityList)
            {
                rank++;
                if (item.UserID != ctx.User.Id) continue;
                User userInfo = await DatabaseContext.Users.FirstOrDefaultAsync(w => w.DiscordId == ctx.User.Id);
                personalScore.Append($"⭐Rank: {rank}\t Points: {item.Points}");
                if (userInfo == null) continue;
                personalScore.AppendLine($"\t🍪:{userInfo.CookiesTaken}/{userInfo.CookiesGiven}");
                break;
            }
            stringBuilder.AppendLine($"\n# Your Ranking\n{personalScore.ToString()}\n```");
            return stringBuilder.ToString();
        }

        [SlashRequireGuild]
        [SlashCommand("cookie", "Gives a user a cookie.")]
        public async Task Cookie(InteractionContext ctx, [Option("User", "Who to give the cookie to")] DiscordUser member)
        {
            await ctx.DeferAsync(true);
            if (ctx.Member == member)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You can't give yourself a cookie"));
                return;
            }

            User giver = await DatabaseContext.Users.FindAsync(ctx.Member.Id) ?? await DatabaseMethodService.AddUserAsync(new User(ctx.Member.Id));
            User receiver = await DatabaseContext.Users.FindAsync(member.Id) ?? await DatabaseMethodService.AddUserAsync(new User( member.Id));
            await DatabaseContext.SaveChangesAsync();
            
            if (giver.CookieDate.Date == DateTime.UtcNow.Date)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Your cookie box is empty. You can give a cookie in {24-DateTime.UtcNow.Hour} Hours, {(59-DateTime.UtcNow.Minute)-1} Minutes, {(59-DateTime.UtcNow.Second)} Seconds."));
                return;
            }

            giver.CookieDate = DateTime.UtcNow.Date;
            giver.CookiesGiven++;
            receiver.CookiesTaken++;
            
            DatabaseContext.Users.UpdateRange(giver,receiver);
            await DatabaseContext.SaveChangesAsync();

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Cookie given."));

            await new DiscordMessageBuilder()
                .WithContent($"{member.Mention}, {ctx.Member.Username} has given you a :cookie:")
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [SlashRequireGuild,
        SlashCommand("submit-photo", "Submit a photo to the photo competition.")]
        public async Task SubmitPhoto(InteractionContext ctx,
            [Autocomplete(typeof (PhotoContestOption)), Option("Competition", "To which competition to submit to.")] long competitionId,
            [Option("Photo", "The photo to submit.")] DiscordAttachment image)
        {
            await ctx.DeferAsync(true);
            
            Guild guild = await DatabaseContext.Guilds
                .Include(x => x.PhotoCompSettings)
                .ThenInclude(x=>x.Entries)
                .FirstOrDefaultAsync(x => x.Id == ctx.Guild.Id);
            PhotoCompSettings competitionSettings = guild.PhotoCompSettings.FirstOrDefault(x=>x.Id == competitionId);
            if (competitionSettings is null || competitionSettings.IsOpen is false)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"The competition is not open. Or you have provided an invalid competition ID. Please try again."));
                return;
            }
            if (competitionSettings.MaxEntries != 0 && competitionSettings.Entries.Count(x => x.UserId==ctx.User.Id) >= competitionSettings.MaxEntries)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You have reached the maximum amount of entries for this competition. You can submit a maximum of {competitionSettings.MaxEntries} entries."));
                return;
            }
            var excludedParameters = guild.PhotoCompSettings
                .Where(x=>x.IsOpen && x.Entries.Any(entry=>entry.UserId==ctx.User.Id))
                .Select(x=>x.CustomParameter).ToImmutableArray();
            if (excludedParameters.Any(customParameter=>customParameter==competitionSettings.CustomParameter))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You have already submitted to this competition. Please try again."));
                return;
            }
            
            DiscordChannel dumpChannel = ctx.Guild.GetChannel(competitionSettings.DumpChannelId);
            DiscordMessageBuilder messageBuilder = new();
            DiscordEmbedBuilder embedBuilder = new()
            {
                Description = $"# 📷 {ctx.User.Username} submitted a photo\n" +
                              $"- User: {ctx.User.Mention}({ctx.User.Id})\n" +
                              $"- Competition: {competitionSettings.CustomName}\n" +
                              $"- Date: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>"
            };
            
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(image.Url);
            string fileName = Guid.NewGuid() + image.FileName;
            List<string> imageExtensions = new() { ".png", ".jpg", ".jpeg"};
            if (!imageExtensions.Contains(Path.GetExtension(fileName)))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Unsupported file format, make sure your image is of type .png, .jpg or .jpeg"));
                return;
            }
            if (response.IsSuccessStatusCode)
            {
                var fileStream = new MemoryStream();
                await response.Content.CopyToAsync(fileStream);
                fileStream.Position = 0;
                messageBuilder.AddFile(fileName, fileStream);
                embedBuilder.ImageUrl = $"attachment://{fileName}";
                messageBuilder.AddEmbed(embedBuilder);
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Failed to download image. Please try again."));
                return;
            }
            DiscordMessage message = await messageBuilder.SendAsync(dumpChannel);
            message = await dumpChannel.GetMessageAsync(message.Id);
            await DatabaseMethodService.AddPhotoCompEntryAsync(new PhotoCompEntries(ctx.User.Id, competitionSettings.Id,
                message.Embeds[0].Image.Url.ToString(), DateTime.UtcNow));
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent(
                    $"Photo submitted to \"{competitionSettings.CustomName}\" competition."));
        }
        
        public sealed class PhotoContestOption : IAutocompleteProvider
        {
            public async Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                Guild guildSettings = await databaseContext.Guilds
                    .Include(x => x.PhotoCompSettings)
                    .ThenInclude(x=>x.Entries)
                    .FirstOrDefaultAsync(x => x.Id == ctx.Guild.Id);
                var customParameters = guildSettings.PhotoCompSettings
                    .Where(x=>x.IsOpen && x.Entries.Any(entry=>entry.UserId==ctx.User.Id))
                    .Select(x=>x.CustomParameter).ToImmutableArray();
                var openCompetitions = guildSettings.PhotoCompSettings
                    .Where(x => x.IsOpen && !customParameters.Any(customParameter=>customParameter==x.CustomParameter))
                    .ToImmutableArray();
                
                if (openCompetitions.Length==0)
                {
                    return new DiscordAutoCompleteChoice[]
                        { new DiscordAutoCompleteChoice("No open competitions", -1) };
                }
                return openCompetitions.Select(photoCompSettings => new DiscordAutoCompleteChoice(photoCompSettings.CustomName, photoCompSettings.Id)).ToList();
            }
        }
    }
}