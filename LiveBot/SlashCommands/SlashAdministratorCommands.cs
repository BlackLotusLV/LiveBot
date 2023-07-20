using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Admin","Administrator commands.", false)]
    [SlashRequireGuild]
    [SlashRequireBotPermissions(Permissions.ManageGuild)]
    internal sealed class SlashAdministratorCommands : ApplicationCommandModule
    {
        public LiveBotDbContext DatabaseService { private get; set; }
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
        [SlashCommand("start-photo-comp", "Starts a photo competition")]
        public async Task StartPhotoComp(InteractionContext ctx,
            [Option("Channel", "Channel where to send the message")] DiscordChannel channel,
            [Option("Winner-Count", "How many winners should be selected")] long winnerCount,
            [Option("Max-Entries", "How many entries can be submitted")] long maxEntries,
            [Option("Custom-Parameter", "Custom parameter for the competition")] long customParameter,
            [Option("Custom-Name", "Custom name for the competition")] string customName)
        {
            await ctx.DeferAsync(true);
            var photoCompSettings = new PhotoCompSettings(ctx.Guild.Id)
            {
                WinnerCount = (int)winnerCount,
                MaxEntries = (int)maxEntries,
                CustomParameter = (int)customParameter,
                CustomName = customName,
                DumpChannelId = channel.Id,
                IsOpen = true
            };
            await DatabaseService.PhotoCompSettings.AddAsync(photoCompSettings);
            await DatabaseService.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Photo competition started"));
        }
        [SlashCommand("end-photo-comp", "Ends a photo competition")]
        public async Task EndPhotoComp(InteractionContext ctx,
            [Autocomplete(typeof(SlashCommands.PhotoContestOption)), Minimum(0),Option("Competition","Which competition to close")]long photoCompId)
        {
            await ctx.DeferAsync(true);
            PhotoCompSettings photoCompSettings = await DatabaseService.PhotoCompSettings.FindAsync((int)photoCompId);
            if (photoCompSettings == null || photoCompSettings.GuildId != ctx.Guild.Id || photoCompSettings.IsOpen == false)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No photo competition found"));
                return;
            }
            photoCompSettings.IsOpen = false;
            await DatabaseService.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Photo competition ended"));
        }
    }
}
