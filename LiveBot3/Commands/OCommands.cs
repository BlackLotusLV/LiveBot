using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Net.Http;

namespace LiveBot.Commands
{
    [Group("!")]
    [Description("Owner commands")]
    [Hidden]
    [RequireOwner]
    internal class OCommands : BaseCommandModule
    {
        [Command("react")]
        public async Task React(CommandContext ctx, DiscordMessage message, params DiscordEmoji[] emotes)
        {
            foreach (DiscordEmoji emote in emotes)
            {
                await message.CreateReactionAsync(emote);
                await Task.Delay(300);
            }
            await ctx.Message.DeleteAsync();
        }

        [Command("buttonmessage")]
        [Aliases("buttonmsg")]
        public async Task ButtonMessage(CommandContext ctx,
            DiscordChannel channel,
            [Description("First message content, split by |, then button components split by, and then each button by |\ncustom id, lable, emoji()")][RemainingText] string rawData)
        {
            await ctx.TriggerTypingAsync();
            string[] splitData = rawData.Split('|');
            List<DiscordComponent> buttons = new();

            for (int i = 1; i < splitData.Length; i++)
            {
                string[] ButtonComponents = splitData[i].Split(',');
                buttons.Add(new DiscordButtonComponent(ButtonStyle.Primary, ButtonComponents[0], ButtonComponents[1], false, UInt64.TryParse(ButtonComponents[2], out ulong emojiID) ? new DiscordComponentEmoji(emojiID) : null));
            }
            await new DiscordMessageBuilder()
                .WithContent(splitData[0])
                .AddComponents(buttons)
                .SendAsync(channel);
        }

        [Command("editmessage")]
        [Aliases("editmsg")]
        public async Task EditMessage(CommandContext ctx, DiscordMessage message, [RemainingText] string text)
        {
            await message.ModifyAsync(text.Replace("`", ""));
            await ctx.Message.DeleteAsync();
        }

        [Command("update")]
        public async Task Update(CommandContext ctx, [Description("Which database to update. (All will update all db)")] string db = "default")
        {
            await ctx.TriggerTypingAsync();
            string msgcontent;
            switch (db.ToLower())
            {
                case "all":
                    DB.DBLists.LoadAllLists();
                    msgcontent = "All lists updated";
                    break;

                case "vehicle":
                case "vehicles":
                    DB.DBLists.LoadVehicleList();
                    msgcontent = "Vehicle list updated";
                    break;

                case "server":
                    DB.DBLists.LoadServerSettings();
                    msgcontent = "Server settings list updated";
                    break;

                case "bannedw":
                case "banword":
                case "bword":
                    DB.DBLists.LoadBannedWords();
                    msgcontent = "Banned Words list updated";
                    break;

                default:
                    msgcontent = "Couldn't find this table. Nothing was updated\n" +
                        "all - updates all tables\n" +
                        "vehicle - updates the **vehicle list**\n" +
                        "server - updates Server Settings\n" +
                        "bannedw - Update banned words list";
                    break;
            }
            DiscordMessage msg = await ctx.RespondAsync(msgcontent);
            await Task.Delay(10000);
            await msg.DeleteAsync();
        }

        [Command("updatehub")]
        public async Task UpdateHub(CommandContext ctx)
        {
            await HubMethods.UpdateHubInfo(true);
            DiscordMessage msg = await ctx.RespondAsync("TCHub info has been force updated.");
            await Task.Delay(10000).ContinueWith(f => msg.DeleteAsync());
        }

        [Command("stopbot")]
        public async Task StopBot(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();
            System.Environment.Exit(0);
        }

        [Command("getguilds")]
        public async Task GetGuilds(CommandContext ctx)
        {
            StringBuilder sb = new();
            foreach (var guild in Program.Client.Guilds.Values)
            {
                sb.AppendLine($"{guild.Name} ({guild.Id})");
            }
            await ctx.RespondAsync(sb.ToString());
        }

        [Command("leaveguild")]
        public async Task LeaveGuild(CommandContext ctx, DiscordGuild guild)
        {
            await guild.LeaveAsync();
            await ctx.RespondAsync($"The bot has left {guild.Name} guild!");
        }
    }
}