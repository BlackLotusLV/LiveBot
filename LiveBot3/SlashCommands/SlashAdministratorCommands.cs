using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using LiveBot.Services;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Admin","Administrator commands.", false)]
    [SlashRequireGuild]
    [SlashRequireBotPermissions(Permissions.ManageGuild)]
    internal class SlashAdministratorCommands : ApplicationCommandModule
    {
        public ITheCrewHubService TheCrewHubService { private get; set; }
        [SlashCommand("Say","Bot says a something")]
        public async Task Say(InteractionContext ctx, [Option("Message", "The message what the bot should say.")] string message, [Option("Channel", "Channel where to send the message")] DiscordChannel channel = null)
        {
            await ctx.DeferAsync(true);
            if (channel==null)
            {
                channel = ctx.Channel;
            }
            await channel.SendMessageAsync(message);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Message has been sent"));
        }

        [SlashCommand("update-hub", "Force updates the crew hub cache")]
        public async Task UpdateHub(InteractionContext ctx)
        {
            await ctx.DeferAsync((true));
            await TheCrewHubService.GetSummitDataAsync(true);
            await TheCrewHubService.GetGameDataAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Hub info updated"));
        }
    }
}
