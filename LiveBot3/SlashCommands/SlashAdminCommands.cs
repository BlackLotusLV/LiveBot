﻿using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Admin", "Admin commands")]
    [SlashRequirePermissions(Permissions.KickMembers)]
    internal class SlashAdminCommands : ApplicationCommandModule
    {
        [SlashCommand("warn", "Warn a user.")]
        [SlashRequireGuild]
        public async Task Warning(InteractionContext ctx,
            [Option("user", "User to warn")] DiscordUser user,
            [Option("reason", "Why the user is being warned")] string reason)
        {
            await ctx.DeferAsync(true);
            await Services.WarningService.WarnUserAsync(user, ctx.Member, ctx.Guild, ctx.Channel, reason, false, ctx);
        }

        [SlashCommand("unwarn", "Removes a warning from the user")]
        public async Task RemoveWarning(InteractionContext ctx,
            [Option("user", "User to remove the warning for")] DiscordUser user,
            [Option("Warning_ID", "The ID of a specific warning. Leave as is if don't want a specific one", true)] long WarningID = -1)
        {
            await ctx.DeferAsync(true);
            var WarnedUserStats = DB.DBLists.ServerRanks.FirstOrDefault(f => ctx.Guild.Id == f.Server_ID && user.Id == f.User_ID);
            var ServerSettings = DB.DBLists.ServerSettings.FirstOrDefault(f => ctx.Guild.Id == f.ID_Server);
            var Warnings = DB.DBLists.Warnings.Where(f => ctx.Guild.Id == f.Server_ID && user.Id == f.User_ID).ToList();
            StringBuilder modmsgBuilder = new();
            DiscordMember member = null;
            if (ServerSettings.WKB_Log == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This server has not set up this feature."));
                return;
            }
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch (Exception)
            {
                modmsgBuilder.AppendLine($"{user.Mention} is no longer in the server.");
            }

            DiscordChannel modlog = ctx.Guild.GetChannel(Convert.ToUInt64(ServerSettings.WKB_Log));
            if (WarnedUserStats is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"This user, {user.Username}, has no warning history."));
                return;
            }
            if (WarnedUserStats.Warning_Level == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"This user, {user.Username}, warning level is already 0."));
                return;
            }

            WarnedUserStats.Warning_Level -= 1;
            DB.Warnings entry = Warnings.FirstOrDefault(f => f.Active is true && f.ID_Warning == WarningID);
            if (entry is null)
            {
                entry = Warnings.Where(f => f.Active is true).OrderBy(f => f.ID_Warning).FirstOrDefault();
            }
            entry.Active = false;
            DB.DBLists.UpdateWarnings(entry);
            DB.DBLists.UpdateServerRanks(WarnedUserStats);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Warning level lowered for {user.Username}"));

            string Description = $"{user.Mention} has been unwarned by {ctx.User.Mention}. Warning level now {WarnedUserStats.Warning_Level}";
            try
            {
                await member.SendMessageAsync($"Your warning level in **{ctx.Guild.Name}** has been lowered to {WarnedUserStats.Warning_Level} by {ctx.User.Mention}");
            }
            catch
            {
                modmsgBuilder.AppendLine($"{user.Mention} could not be contacted via DM.");
            }

            await CustomMethod.SendModLog(modlog, user, Description, CustomMethod.ModLogType.Unwarn, modmsgBuilder.ToString());
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
        [SlashCommand("Infractions", "Shows the infractions of the user")]
        public async Task Infractions(InteractionContext ctx, [Option("user", "User to show the infractions for")] DiscordUser user)
        {
            await ctx.DeferAsync();
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(CustomMethod.GetUserWarnings(ctx.Guild, user, true)));
        }
        [SlashCommand("FAQ","Creates a new FAQ message")]
        public async Task FAQ(InteractionContext ctx, [Option("Question", "The question to ask")] string question, [Option("Answer", "The answer to the question")] string answer)
        {
            await ctx.DeferAsync(true);
            await new DiscordMessageBuilder()
                .WithContent($"**Q: {question}**\n *A: {answer.TrimEnd()}*")
                .SendAsync(ctx.Channel);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("FAQ message sent"));
        }

        [SlashCommand("FAQ-Edit", "Edits an existing FAQ message, using the message ID")]
        public async Task FAQEdit(InteractionContext ctx, [Option("Message_ID", "The message ID to edit")] string messageID, [Option("Question", "The question to ask")] string question = null, [Option("Answer", "The answer to the question")] string answer = null)
        {
            await ctx.DeferAsync(true);
            DiscordMessage message = await ctx.Channel.GetMessageAsync(Convert.ToUInt64(messageID));
            string ogMessage = message.Content.Replace("*", string.Empty);
            if (question==null)
            {
                question = ogMessage.Substring(ogMessage.IndexOf(":") + 1,ogMessage.Length-ogMessage.Substring(ogMessage.IndexOf("A:")).Length).TrimStart();
            }
            if (answer == null)
            {
                answer = ogMessage.Substring(ogMessage.IndexOf("A:") + 2).TrimStart();
            }

            await message.ModifyAsync($"**Q: {question}**\n *A: {answer.TrimEnd()}*");
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