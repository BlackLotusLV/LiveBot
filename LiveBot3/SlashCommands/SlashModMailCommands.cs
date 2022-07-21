using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.SlashCommands;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("ModMail","Moderator commands for mod mail",false)]
    internal class SlashModMailCommands : ApplicationCommandModule
    {
        [SlashCommand("Reply","Replies to a specific mod mail")]
        public async Task Reply(InteractionContext ctx, [Autocomplete(typeof(ReplyOption))][Option("ID","Mod Mail Entry ID")] long id, [Option("Response","The message to send to the user")]string reply)
        {
            await ctx.DeferAsync(true);
            DB.ModMail MMEntry = DB.DBLists.ModMail.FirstOrDefault(w => w.ID == id && w.IsActive);
            if (MMEntry == null)
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
                Title = $"[REPLY] #{MMEntry.ID} Mod Mail Response",
                Description = $"{ctx.Member.Username} - {reply}",
                Color = new DiscordColor(MMEntry.ColorHex)
            };
            try
            {
                DiscordMember member = await ctx.Guild.GetMemberAsync(MMEntry.User_ID);
                await member.SendMessageAsync($"{ctx.Member.Username} - {reply}");
            }
            catch (Exception e)
            {
                embed.Description = $"User has left the server, blocked the bot or closed their DMs. Could not send a response!\nHere is what you said `{reply}`";
                embed.Title = $"[ERROR] {embed.Title}";
                Console.WriteLine(e.InnerException);
            }
            MMEntry.LastMSGTime = DateTime.UtcNow;
            DB.DBLists.UpdateModMail(MMEntry);

            DiscordChannel MMChannel = ctx.Guild.GetChannel(DB.DBLists.ServerSettings.FirstOrDefault(w => w.ID_Server == ctx.Guild.Id).ModMailID);
            await MMChannel.SendMessageAsync(embed: embed);

            Program.Client.Logger.LogInformation(CustomLogEvents.ModMail, "An admin has responded to Mod Mail entry #{EntryId}", MMEntry.ID);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Mod mail #{id} reply sent"));
        }

        sealed class ReplyOption : IAutocompleteProvider
        {
             public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                List<DiscordAutoCompleteChoice> result = new();
                foreach (var item in DB.DBLists.ModMail.Where(w => w.Server_ID == ctx.Guild.Id && w.IsActive).Select(item => item.ID))
                {
                    result.Add(new DiscordAutoCompleteChoice($"#{item}", item));
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        [SlashCommand("blacklist", "Blocks a user from using modmail")]
        public async Task ModMailBlock(InteractionContext ctx, [Option("user", "User to block")] DiscordUser user)
        {
            await ctx.DeferAsync(true);
            DB.ServerRanks rank = DB.DBLists.ServerRanks.FirstOrDefault(w => w.Server_ID == ctx.Guild.Id && w.User_ID == user.Id);
            if (rank == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not a member of this server"));
                return;
            }
            if (rank.MM_Blocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is already blocked from using modmail"));
                return;
            }
            rank.MM_Blocked = true;
            DB.DBLists.UpdateServerRanks(rank);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) has been blocked from using modmail"));
        }

        [SlashCommand("unlist", "Unblocks a user from using modmail")]
        public async Task ModMailUnblock(InteractionContext ctx, [Option("user", "User to unblock")] DiscordUser user)
        {
            await ctx.DeferAsync(true);
            DB.ServerRanks rank = DB.DBLists.ServerRanks.FirstOrDefault(w => w.Server_ID == ctx.Guild.Id && w.User_ID == user.Id);
            if (rank == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not a member of this server"));
                return;
            }
            if (!rank.MM_Blocked)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) is not blocked from using modmail"));
                return;
            }
            rank.MM_Blocked = false;
            DB.DBLists.UpdateServerRanks(rank);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{user.Username}({user.Id}) has been unblocked from using modmail"));
        }
    }
}
