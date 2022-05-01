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
        public async Task Warning(InteractionContext ctx, [Option("user", "User to warn")] DiscordUser username, [Option("reason", "Why the user is being warned")] string reason)
        {
            await ctx.DeferAsync(true);
            await Services.WarningService.WarnUserAsync(username, ctx.Member, ctx.Guild, ctx.Channel, reason, false, ctx);
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