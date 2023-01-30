using DSharpPlus.SlashCommands;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("ModMail","Moderator commands for mod mail",false)]
    internal abstract class SlashModMailCommands : ApplicationCommandModule
    {
        private readonly LiveBotDbContext _databaseContext;
        private readonly IModMailService _modMailService;

        SlashModMailCommands(LiveBotDbContext databaseContext, IModMailService modMailService)
        {
            _databaseContext = databaseContext;
            _modMailService = modMailService;
        }
        
        [SlashCommand("Reply","Replies to a specific mod mail")]
        public async Task Reply(InteractionContext ctx, [Autocomplete(typeof(ActiveModMailOption))][Option("ID","Mod Mail Entry ID")] long id, [Option("Response","The message to send to the user")]string reply)
        {
            await ctx.DeferAsync(true);
            ModMail entry = await _databaseContext.ModMail.FirstOrDefaultAsync(x => x.ModMailId == id && x.IsActive);
            if (entry == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find an active entry with this ID."));
                return;
            }
            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = ctx.User.AvatarUrl,
                    Name = ctx.User.Username
                },
                Title = $"[REPLY] #{entry.ModMailId} Mod Mail Response",
                Description = $"{ctx.Member.Username} - {reply}",
                Color = new DiscordColor(entry.ColorHex)
            };
            try
            {
                DiscordMember member = await ctx.Guild.GetMemberAsync(entry.UserDiscordId);
                await member.SendMessageAsync($"{ctx.Member.Username} - {reply}");
            }
            catch (Exception e)
            {
                embed.Description = $"User has left the server, blocked the bot or closed their DMs. Could not send a response!\nHere is what you said `{reply}`";
                embed.Title = $"[ERROR] {embed.Title}";
                Console.WriteLine(e.InnerException);
            }

            await _databaseContext.ModMail
                .Where(x => x.ModMailId == entry.ModMailId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastMessageTime, x => DateTime.UtcNow));

            DiscordChannel mmChannel = ctx.Guild.GetChannel(_databaseContext.ServerSettings.First(w => w.GuildId == ctx.Guild.Id).ModMailChannelId);
            await mmChannel.SendMessageAsync(embed: embed);

            ctx.Client.Logger.LogInformation(CustomLogEvents.ModMail, "An admin has responded to Mod Mail entry #{EntryId}", entry.ModMailId);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Mod mail #{id} reply sent"));
        }

        sealed class ActiveModMailOption : IAutocompleteProvider
        {
             public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                List<DiscordAutoCompleteChoice> result = new();
                foreach (long item in databaseContext.ModMail.Where(w => w.GuildId == ctx.Guild.Id && w.IsActive).Select(item => item.ModMailId))
                {
                    result.Add(new DiscordAutoCompleteChoice($"#{item}", item));
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        [SlashCommand("close","Closes a Mod Mail entry")]
        public async Task Close(InteractionContext ctx, [Autocomplete(typeof(ActiveModMailOption))][Option("ID", "Mod Mail Entry ID")] long id)
        {
            await ctx.DeferAsync(true); 
            ModMail entry = await _databaseContext.ModMail.FirstOrDefaultAsync(w => w.ModMailId == id && w.IsActive);
            if (entry == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find an active entry with this ID."));
                return;
            }
            await _modMailService.CloseModMailAsync(ctx.Client,entry,ctx.User, $" Mod Mail closed by {ctx.User.Username}",$"**Mod Mail closed by {ctx.User.Username}!\n----------------------------------------------------**");
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"ModMail entry #{id} closed."));
        }

        [SlashCommand("block", "Blocks a user from using modmail")]
        public async Task ModMailBlock(InteractionContext ctx, [Option("user", "User to block")] DiscordUser user)
        {
            await ctx.DeferAsync(true);
            ServerRanks rank = _databaseContext.ServerRanks.FirstOrDefault(w => w.GuildId == ctx.Guild.Id && w.UserDiscordId == user.Id);
            if (rank == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not a member of this server"));
                return;
            }
            if (rank.IsModMailBlocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is already blocked from using modmail"));
                return;
            }

            await _databaseContext.ServerRanks
                .Where(x => x.UserDiscordId == user.Id && x.GuildId == ctx.Guild.Id)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsModMailBlocked, x => true));
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) has been blocked from using modmail"));
        }

        [SlashCommand("unblock", "Unblocks a user from using modmail")]
        public async Task ModMailUnblock(InteractionContext ctx, [Option("user", "User to unblock")] DiscordUser user)
        {
            await ctx.DeferAsync(true);
            ServerRanks rank = await _databaseContext.ServerRanks.FirstAsync(w => w.GuildId == ctx.Guild.Id && w.UserDiscordId == user.Id);
            if (rank == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not a member of this server"));
                return;
            }
            if (!rank.IsModMailBlocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not blocked from using modmail"));
                return;
            }
            await _databaseContext.ServerRanks
                .Where(x => x.UserDiscordId == user.Id && x.GuildId == ctx.Guild.Id)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsModMailBlocked, x => false));
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) has been unblocked from using modmail"));
        }
    }
}
