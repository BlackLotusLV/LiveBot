using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Globalization;
using System.Net.Http;
using LiveBot.DB;

namespace LiveBot.Commands
{
    public class UngroupedCommands : BaseCommandModule
    {
        [Command("getemote")]
        [Description("Returns the ID of an emote")]
        public async Task GetEmote(CommandContext ctx, params DiscordEmoji[] emotes)
        {
            StringBuilder sb = new();

            foreach (DiscordEmoji emoji in emotes)
            {
                sb.AppendLine($"{emoji} - {emoji.Id}");
            }
            await new DiscordMessageBuilder()
                .WithContent(sb.ToString())
                .WithReply(ctx.Message.Id, true)
                .SendAsync(ctx.Channel);
        }

        [Command("ping")]
        [Description("Shows that the bots response time")]
        [Aliases("pong")]
        public async Task Ping(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();
            await ctx.RespondAsync($"Pong! {ctx.Client.Ping}ms");
        }

        [Command("share")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [Description("Informs the user, how to get access to the share channels.")] // photomode info command
        public async Task Share(CommandContext ctx,
            [Description("Specifies the user the bot will mention, use ID or mention the user. If left blank, it will mention you.")] DiscordMember username = null,
            [Description("Specifies in what language the bot will respond. example, fr-french")] string language = null)
        {
            await ctx.Message.DeleteAsync(); //deletes command message
            await ctx.TriggerTypingAsync();
            string content;
            if (username is null) //checks if user name is not specified
            {
                content = CustomMethod.GetCommandOutput(ctx, "share", language, ctx.Member);
            }
            else // if user name specified
            {
                content = CustomMethod.GetCommandOutput(ctx, "share", language, username);
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("platform")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [Description("Informs the user about the platform roles.")] //platform selection command
        public async Task Platform(CommandContext ctx, [Description("Specifies the user the bot will mention, use ID or mention the user. If left blank, it will mention you.")] DiscordMember username = null, [Description("Specifies in what language the bot will respond. example, fr-french")] string language = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            string content;
            if (username is null)
            {
                content = CustomMethod.GetCommandOutput(ctx, "platform", language, ctx.Member);
            }
            else
            {
                content = CustomMethod.GetCommandOutput(ctx, "platform", language, username);
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("maxlvl")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [Description("Explains how to get maximum car level in The Crew 1.")] // how to get max level for cars in TC1
        public async Task MaxCarlvl(CommandContext ctx, [Description("Specifies the user the bot will mention, use ID or mention the user. If left blank, it will mention you.")] DiscordMember username = null, [Description("Specifies in what language the bot will respond. example, fr-french")] string language = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            string content;
            if (username is null)
            {
                content = CustomMethod.GetCommandOutput(ctx, "maxlvl", language, ctx.Member);
            }
            else
            {
                content = CustomMethod.GetCommandOutput(ctx, "maxlvl", language, username);
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("lfc")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [Description("Informs the user of using the LFC channels, or to get the platform role if they don't have it.")]
        public async Task LFC(CommandContext ctx, DiscordMember username = null, [Description("Specifies in what language the bot will respond. example, fr-french")] string language = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            string content = CustomMethod.GetCommandOutput(ctx, "lfc2", language, username);
            DiscordRole pc = ctx.Guild.GetRole(223867454642716673);
            DiscordRole ps = ctx.Guild.GetRole(223867009484587008);
            DiscordRole xb = ctx.Guild.GetRole(223867264246611970);
            bool check = false;
            if (username == null)
            {
                username = ctx.Member;
            }
            foreach (var item in username.Roles)
            {
                if ((item == pc || item == ps || item == xb) && !check)
                {
                    content = CustomMethod.GetCommandOutput(ctx, "lfc1", language, username);
                    check = true;
                }
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("it")]
        [Description("Sends the IT Crowd image of \"have you tried turning it off and on again?\"")]
        public async Task IT(CommandContext ctx, DiscordMember username = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            FileStream ITImage = new("Assets/img/ITC.jpg", FileMode.Open);
            string content;
            if (username == null)
            {
                content = $"{ctx.User.Mention}";
            }
            else
            {
                content = $"{username.Mention}";
            }

            await new DiscordMessageBuilder()
                .AddFile(ITImage)
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("bs")]
        public async Task BS(CommandContext ctx, DiscordMember discordMember = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            FileStream image = new("Assets/img/bs.gif", FileMode.Open);
            await new DiscordMessageBuilder()
                .AddFile(image)
                .WithAllowedMention(new UserMention())
                .WithContent($"{(discordMember == null ? ctx.Member.Mention : discordMember.Mention)}")
                .SendAsync(ctx.Channel);
        }

        [Command("supra")]
        [Description("Sends the supra gif.")]
        [Cooldown(1, 60, CooldownBucketType.Channel)]
        public async Task Supra(CommandContext ctx, DiscordMember username = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            string content;
            if (username is null)
            {
                content = CustomMethod.GetCommandOutput(ctx, "supra", null, ctx.Member);
            }
            else
            {
                content = CustomMethod.GetCommandOutput(ctx, "supra", null, username);
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("support")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [Description("Gives the link to the support page")]
        public async Task Support(CommandContext ctx, DiscordMember username = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            string content;
            if (username is null)
            {
                content = CustomMethod.GetCommandOutput(ctx, "support", null, ctx.Member);
            }
            else
            {
                content = CustomMethod.GetCommandOutput(ctx, "support", null, username);
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("forums")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [Description("Gives the link to the forum")]
        public async Task Forums(CommandContext ctx, DiscordMember username = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            string content;
            if (username is null)
            {
                content = CustomMethod.GetCommandOutput(ctx, "forums", null, ctx.Member);
            }
            else
            {
                content = CustomMethod.GetCommandOutput(ctx, "forums", null, username);
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("prosettings")]
        [Aliases("psettings")]
        [Cooldown(1, 10, CooldownBucketType.Channel)]
        [Description("Explains how to find pro settings")]
        public async Task ProSettings(CommandContext ctx, DiscordMember username = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            string content;
            if (username is null)
            {
                content = CustomMethod.GetCommandOutput(ctx, "prosettings", null, ctx.Member);
            }
            else
            {
                content = CustomMethod.GetCommandOutput(ctx, "prosettings", null, username);
            }
            await new DiscordMessageBuilder()
                .WithContent(content)
                .WithAllowedMention(new UserMention())
                .SendAsync(ctx.Channel);
        }

        [Command("info")]
        [Description("Shows users discord info")]
        public async Task Info(CommandContext ctx, [Description("users ID or mention")] DiscordMember user = null)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();
            if (user == null)
            {
                user = ctx.Member;
            }
            string format = "dddd, MMM dd yyyy HH:mm:ss zzzz";
            CultureInfo info = new("en-GB");
            string joinedstring = user.JoinedAt.ToString(format, info);
            string createdstring = user.CreationTimestamp.ToString(format, info);
            DiscordEmbedBuilder embed = new()
            {
                Color = new DiscordColor(0xFF6600),
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = user.Username,
                    IconUrl = user.AvatarUrl
                },
                Description = $"**ID**\t\t\t\t\t\t\t\t\t\t\t\t**Nickname**\n" +
                $"{user.Id}\t\t\t{user.Nickname ?? "*none*"}\n" +
                $"**Account created**\n" +
                $"{createdstring}\n" +
                $"**Join date**\n" +
                $"{joinedstring}\n",
                Title = "User info",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = user.AvatarUrl
                }
            };
            await ctx.RespondAsync(embed: embed);
        }

        [Command("convert")]
        [Description("Converts M to KM and KM to M")]
        public async Task Convert(CommandContext ctx,
            [Description("value of the speed you want to convert")] double value,
            [Description("Mesurment of speed from what you convert")] string mesurement)
        {
            double result;
            mesurement = mesurement.ToLower();
            const double ToMiles = 0.621371192;
            const double ToKilometers = 1.609344;
            switch (mesurement)
            {
                case "km":
                case "kilometers":
                case "kmh":
                case "k":
                    result = value * ToMiles;
                    await ctx.RespondAsync($"{value} Kilometers = {result} Miles");
                    break;

                case "miles":
                case "mph":
                case "m":
                    result = value * ToKilometers;
                    await ctx.RespondAsync($"{value} Miles = {result} Kilometers");
                    break;
            }
        }

        [Command("status")]
        [Description("The Crew 2 Server status.")]
        [Cooldown(1, 120, CooldownBucketType.User)]
        public async Task Status(CommandContext ctx)
        {
            string HTML;
            using (HttpClient wc = new())
            {
                HTML = await wc.GetStringAsync($"https://ubistatic-a.akamaihd.net/0115/tc2/status.html");
            }
            if (HTML.Contains("STATUS OK"))
            {
                await ctx.RespondAsync("The Crew 2 Server is Online");
            }
            else
            {
                await ctx.RespondAsync("The Crew 2 Server is Offline");
            }
        }
    }
}