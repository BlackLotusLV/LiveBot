using DSharpPlus.SlashCommands;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("ModMail","Moderator commands for mod mail",false)]
    internal class SlashModMailCommands : ApplicationCommandModule
    {
        public LiveBotDbContext DatabaseContext { private get; set; }
        public IModMailService ModMailService { private get; set; }
        
        [SlashCommand("Reply","Replies to a specific mod mail")]
        public async Task Reply(InteractionContext ctx, [Autocomplete(typeof(ActiveModMailOption))][Option("ID","Mod Mail Entry ID")] long id, [Option("Response","The message to send to the user")]string reply)
        {
            await ctx.DeferAsync(true);
            ModMail entry = await DatabaseContext.ModMail.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
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
                Title = $"[REPLY] #{entry.Id} Mod Mail Response",
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

            await DatabaseContext.ModMail
                .Where(x => x.Id == entry.Id)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastMessageTime, x => DateTime.UtcNow));

            Guild guild = await DatabaseContext.Guilds.FindAsync(ctx.Guild.Id);
            
            if (guild?.ModMailChannelId != null)
            {
                
                ulong channelId = guild.ModMailChannelId.Value;
                DiscordChannel mmChannel = ctx.Guild.GetChannel(channelId);
                await mmChannel.SendMessageAsync(embed: embed);
                ctx.Client.Logger.LogInformation(CustomLogEvents.ModMail, "An admin has responded to Mod Mail entry #{EntryId}", entry.Id);

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Mod mail #{id} reply sent"));
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed).WithContent("Mod mail channel not specified in server settings, sending reply here"));
            }
        }

        private sealed class ActiveModMailOption : IAutocompleteProvider
        {
             public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                List<DiscordAutoCompleteChoice> result = new();
                foreach (long item in databaseContext.ModMail.Where(w => w.GuildId == ctx.Guild.Id && w.IsActive).Select(item => item.Id))
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
            ModMail entry = await DatabaseContext.ModMail.FindAsync(id);
            if (entry is not { IsActive: true })
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find an active entry with this ID."));
                return;
            }
            await ModMailService.CloseModMailAsync(ctx.Client,entry,ctx.User, $" Mod Mail closed by {ctx.User.Username}",$"**Mod Mail closed by {ctx.User.Username}!\n----------------------------------------------------**");
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"ModMail entry #{id} closed."));
        }

        [SlashCommand("block", "Blocks a user from using modmail")]
        public async Task ModMailBlock(InteractionContext ctx, [Option("user", "User to block")] DiscordUser user)
        {
            await ctx.DeferAsync(true);
            GuildUser guildUser = await DatabaseContext.GuildUsers.FindAsync(new object[] { user.Id, ctx.Guild.Id });
            if (guildUser == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not a member of this server"));
                return;
            }
            if (guildUser.IsModMailBlocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is already blocked from using modmail"));
                return;
            }

            guildUser.IsModMailBlocked = true;
            DatabaseContext.GuildUsers.Update(guildUser);
            await DatabaseContext.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) has been blocked from using modmail"));
        }

        [SlashCommand("unblock", "Unblocks a user from using modmail")]
        public async Task ModMailUnblock(InteractionContext ctx, [Option("user", "User to unblock")] DiscordUser user)
        {
            await ctx.DeferAsync(true);
            GuildUser guildUser = await DatabaseContext.GuildUsers.FindAsync(new object[]{user.Id,ctx.Guild.Id});
            if (guildUser == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not a member of this server"));
                return;
            }
            if (!guildUser.IsModMailBlocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not blocked from using modmail"));
                return;
            }
            guildUser.IsModMailBlocked = false;
            DatabaseContext.GuildUsers.Update(guildUser);
            await DatabaseContext.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) has been unblocked from using modmail"));
        }
    }
}
