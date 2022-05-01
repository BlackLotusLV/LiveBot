using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Admin", "Admin commands")]
    [SlashRequirePermissions(Permissions.KickMembers)]
    internal class SlashAdminCommands : ApplicationCommandModule
    {
        [SlashCommand("warn", "Warn a user.")]
        [SlashRequireGuild]
        public async Task Warning(InteractionContext ctx, [Option("user", "User to warn")] DiscordUser user, [Option("reason", "Why the user is being warned")] string reason)
        {
            await ctx.DeferAsync(true);
            await Services.WarningService.WarnUserAsync(user, ctx.Member, ctx.Guild, ctx.Channel, reason, false, ctx);
        }
        [SlashCommand("Prune", "Prune the message in the channel")]
        public async Task Prune(InteractionContext ctx,
            [Option("Message_Count", "The amount of messages to delete (1-100)")] long MessageCount)
        {
            await ctx.DeferAsync(true);
            if (MessageCount > 100)
            {
                MessageCount = 100;
            }
            IReadOnlyList<DiscordMessage> messageList = await ctx.Channel.GetMessagesAsync((int)MessageCount);
            await ctx.Channel.DeleteMessagesAsync(messageList);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Selected messages have been pruned"));
        }

        [SlashCommand("AddNote","Adds a note in the database without warning the user")]
        public async Task AddNote(InteractionContext ctx, [Option("user", "User to who to add the note to")] DiscordUser user, [Option("Note","Contents of the note.")] string note)
        {
            await ctx.DeferAsync(true);
            DB.Warnings newEntry = new()
            {
                Server_ID = ctx.Guild.Id,
                Active = false,
                Admin_ID = ctx.User.Id,
                Type = "note",
                User_ID = user.Id,
                Time_Created = DateTime.UtcNow,
                Reason = note
            };
            DB.DBLists.InsertWarnings(newEntry);
            DB.ServerSettings serverSettings = DB.DBLists.ServerSettings.FirstOrDefault(w => w.ID_Server == ctx.Guild.Id);
            if (serverSettings.WKB_Log != 0)
            {
                DiscordChannel channel = ctx.Guild.GetChannel(Convert.ToUInt64(serverSettings.WKB_Log));
                await CustomMethod.SendModLog(channel, user, $"**Note added to:**\t{user.Mention}\n**by:**\t{ctx.Member.Username}\n**Note:**\t{note}", CustomMethod.ModLogType.Info);
            }
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{ctx.User.Mention}, a note has been added to {user.Username}({user.Id})"));

        }
        /*
        [SlashCommand("news", "Posts news article to the news channel")]
        [SlashRequireGuild]
        public async Task News(InteractionContext ctx)
        {
            var modal = new DiscordInteractionResponseBuilder().WithTitle("News Post Form").WithCustomId($"modal-{ctx.User.Id}")
            .AddComponents(new TextInputComponent(label: "News Title", customId: "Title", value: ""))
            .AddComponents(new TextInputComponent(label: "Body Text", customId: "Body", value: ""))
            .AddComponents(new TextInputComponent(label: "Media link", customId: "Media", value: "",required: false));

            await ctx.CreateResponseAsync(InteractionResponseType.Modal, modal);
            var interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForModalAsync($"modal-{ctx.User.Id}", ctx.User);
            if (!response.TimedOut)
            {
                var modalInteraction = response.Result.Interaction;
                var values = response.Result.Values;
                DiscordEmbedBuilder embed = new()
                {
                    Author = new DiscordEmbedBuilder.EmbedAuthor()
                    {
                        Name = ctx.Client.CurrentUser.Username,
                        IconUrl = ctx.Client.CurrentUser.AvatarUrl
                    },
                    Title = values["Title"],
                    Description = values["Body"],
                    Color= new DiscordColor(0x59bfff)
                };
                await modalInteraction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embed));
            }
        }
        //*/
    }
}