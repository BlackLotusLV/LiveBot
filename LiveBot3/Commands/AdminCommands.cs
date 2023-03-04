using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Commands
{
    [Group("@")]
    [Description("Administrative commands")]
    [Hidden]
    [RequirePermissions(Permissions.KickMembers)]
    public class AdminCommands : BaseCommandModule
    {
        private readonly IWarningService _warningService;
        private readonly LiveBotDbContext _liveBotDbContext;

        public AdminCommands(IWarningService warningService, LiveBotDbContext liveBotDbContext)
        {
            _warningService = warningService;
            _liveBotDbContext = liveBotDbContext;
        }

        [Command("say")]
        [Description("Bot repeats whatever you tell it to repeat")]
        public async Task Say(CommandContext ctx, DiscordChannel channel, [Description("bot will repeat this")][RemainingText] string word = "")
        {
            await ctx.Message.DeleteAsync();
            await channel.SendMessageAsync(word);
        }

        [Command("vote")]
        [Description("starts a vote")]
        [RequireGuild]
        public async Task Vote(CommandContext ctx, [Description("What to vote about?")][RemainingText] string topic)
        {
            await ctx.Message.DeleteAsync();
            DiscordMessage msg = await new DiscordMessageBuilder()
                .WithContent(topic)
                .SendAsync(ctx.Channel);
            DiscordEmoji up = DiscordEmoji.FromName(ctx.Client, ":thumbsup:");
            DiscordEmoji down = DiscordEmoji.FromName(ctx.Client, ":thumbsdown:");
            await msg.CreateReactionAsync(up);
            await Task.Delay(500);
            await msg.CreateReactionAsync(down);
        }

        [Command("poll")]
        [Description("creates a poll up to 10 choices. Delimiter \".\"")]
        [RequireGuild]
        public async Task Poll(CommandContext ctx, [Description("Options")][RemainingText] string input)
        {
            await ctx.Message.DeleteAsync();
            char delimiter = '.';
            string[] options = input.Split(delimiter);
            string final = "";
            string[] emotename = new string[] { ":zero:", ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:" };
            int i = 0;
            foreach (var item in options)
            {
                final = $"{final}{emotename[i]} {item} \n";
                i++;
            }
            DiscordMessage msg = await new DiscordMessageBuilder()
                .WithContent(final)
                .SendAsync(ctx.Channel);
            DiscordEmoji zero = DiscordEmoji.FromName(ctx.Client, ":zero:");
            DiscordEmoji one = DiscordEmoji.FromName(ctx.Client, ":one:");
            DiscordEmoji two = DiscordEmoji.FromName(ctx.Client, ":two:");
            DiscordEmoji three = DiscordEmoji.FromName(ctx.Client, ":three:");
            DiscordEmoji four = DiscordEmoji.FromName(ctx.Client, ":four:");
            DiscordEmoji five = DiscordEmoji.FromName(ctx.Client, ":five:");
            DiscordEmoji six = DiscordEmoji.FromName(ctx.Client, ":six:");
            DiscordEmoji seven = DiscordEmoji.FromName(ctx.Client, ":seven:");
            DiscordEmoji eight = DiscordEmoji.FromName(ctx.Client, ":eight:");
            DiscordEmoji nine = DiscordEmoji.FromName(ctx.Client, ":nine:");
            DiscordEmoji[] emotes = new DiscordEmoji[] { zero, one, two, three, four, five, six, seven, eight, nine };
            for (int j = 0; j < i; j++)
            {
                await msg.CreateReactionAsync(emotes[j]);
                await Task.Delay(300);
            }
        }

        [Command("rinfo")]
        [Aliases("roleinfo")]
        [Description("give the ID of the role that was mentioned with the command")]
        [RequireGuild]
        public async Task RInfo(CommandContext ctx, DiscordRole role)
        {
            await ctx.RespondAsync($"**Role ID:**{role.Id}");
        }

        [Command("faq")]
        [RequirePermissions(Permissions.BanMembers)]
        [Description("Edits the existing FAQ message")]
        [Priority(10)]
        [RequireGuild]
        public async Task FAQ(CommandContext ctx,
            [Description("FAQ message ID that you want to update")] DiscordMessage faqMsg,
            [Description("What do you want to change? `q` for question, `a` for answer.")] string type,
            [Description("Updated text")][RemainingText] string str1)
        {
            string og = faqMsg.Content;
            og = og.Replace("*", string.Empty);
            string[] str2 = og.Split("A: ");
            str2[0] = str2[0].Remove(str2[0].Length - 2, 2);
            if (type.ToLower() == "q")
            {
                str2[0] = $"Q: {str1}";
            }
            else if (type.ToLower() == "a")
            {
                str2[1] = str1;
            }
            await faqMsg.ModifyAsync($"**{str2[0]}**\n *A: {str2[1].TrimEnd()}*");
            await ctx.Message.DeleteAsync();
        }

        [Command("faq")]
        [RequirePermissions(Permissions.BanMembers)]
        [Description("Creates an FAQ post. Delimiter is `|`")]
        [Priority(9)]
        [RequireGuild]
        public async Task FAQ(CommandContext ctx, [RemainingText] string str1)
        {
            string[] str2 = str1.Split('|');
            await new DiscordMessageBuilder()
                .WithContent($"**Q: {str2[0]}**\n*A: {str2[1].TrimEnd()}*")
                .SendAsync(ctx.Channel);
            await ctx.Message.DeleteAsync();
        }

        [Command("warncount")]
        [Description("Shows the count of warnings issues by each admin")]
        [RequireGuild]
        public async Task WarnCount(CommandContext ctx)
        {
            StringBuilder sb = new();
            int i = 0;
            sb.AppendLine("```csharp\n\t\tUsername");
            foreach (var item in _liveBotDbContext.Infractions.Where(w => w.GuildId == ctx.Guild.Id).GroupBy(w => w.AdminDiscordId)
                .Select(s => new
                {
                    Admin_ID = s.Key,
                    WCount = s.Count(w => w.Type == "warning"),
                    KCount = s.Count(w => w.Type == "kick"),
                    BCount = s.Count(w => w.Type == "ban")
                })
                .OrderByDescending(o => o.WCount))
            {
                i++;
                DiscordUser user = await ctx.Client.GetUserAsync(Convert.ToUInt64(item.Admin_ID));
                sb.AppendLine($"[{i}]\t# {user.Username}\n\tWarnings Issued {item.WCount}\t\tKicks Issued {item.KCount}\t\tBans Issued {item.BCount}");
            }
            sb.AppendLine("```");
            await ctx.RespondAsync(sb.ToString());
        }

        [Command("prune")]
        [Description("Deletes chat history, up to 100 messages per use")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [RequireGuild]
        public async Task Prune(CommandContext ctx, int MessageCount = 1)
        {
            if (MessageCount > 100)
            {
                MessageCount = 100;
            }
            await ctx.Message.DeleteAsync().ContinueWith(t => ctx.TriggerTypingAsync());
            await ctx.Channel.DeleteMessagesAsync(await ctx.Channel.GetMessagesBeforeAsync(ctx.Message.Id, MessageCount));
            DiscordMessage info = await ctx.RespondAsync($"{MessageCount} messages deleted");
            await Task.Delay(5000).ContinueWith(t => info.DeleteAsync());
        }

        [Command("lookup")]
        [Description("Looks up a user by ID")]
        public async Task Lookup(CommandContext ctx, ulong ID)
        {
            await ctx.TriggerTypingAsync();

            DiscordUser user;
            DiscordEmbedBuilder embed = new()
            {
            };
            try
            {
                user = await ctx.Client.GetUserAsync(ID);
                embed.Title = "User found";
                embed.Description = $"User {user.Username} found by ID({user.Id})";
                embed.ImageUrl = user.AvatarUrl;
                await ctx.RespondAsync(embed: embed);
            }
            catch
            {
                await ctx.RespondAsync("Could not find a user by this ID");
            }
        }
    }
}