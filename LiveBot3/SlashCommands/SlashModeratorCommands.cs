using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Mod", "Moderator commands", false)]
    [SlashRequirePermissions(Permissions.KickMembers)]
    [SlashRequireGuild]
    internal class SlashModeratorCommands : ApplicationCommandModule
    {
        public IWarningService WarningService { private get; set; }
        public LiveBotDbContext DatabaseContext { private get; set; }
        public IModMailService ModMailService { private get; set; }
        
        [SlashCommand("warn", "Warn a user.")]
        public async Task Warning(InteractionContext ctx,
            [Option("user", "User to warn")] DiscordUser user,
            [Option("reason", "Why the user is being warned")] string reason)
        {
            await ctx.DeferAsync(true);
            WarningService.AddToQueue(new WarningItem(user, ctx.User, ctx.Guild, ctx.Channel, reason, false, ctx));
        }

        [SlashCommand("remove-warning", "Removes a warning from the user")]
        public async Task RemoveWarning(InteractionContext ctx,
            [Option("user", "User to remove the warning for")] DiscordUser user,
            [Autocomplete(typeof(RemoveWarningOptions))]
            [Option("Warning_ID", "The ID of a specific warning. Leave as is if don't want a specific one", true)] long warningId = -1)
        {
            await ctx.DeferAsync(true);
            await WarningService.RemoveWarningAsync(user, ctx, (int)warningId);
        }
        private sealed class RemoveWarningOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                List<DiscordAutoCompleteChoice> result = new();
                var userId = (ulong)ctx.Options.First(x => x.Name == "user").Value;
                foreach (Infraction item in databaseContext.Infractions.Where(w=>w.GuildId == ctx.Guild.Id && w.UserId == userId && w.Type=="warning" && w.IsActive))
                {
                    result.Add(new DiscordAutoCompleteChoice($"#{item.Id} - {item.Reason}",item.Id));
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        [SlashCommand("Prune", "Prune the message in the channel")]
        public async Task Prune(InteractionContext ctx,
            [Option("Message_Count", "The amount of messages to delete (1-100)")] long messageCount)
        {
            await ctx.DeferAsync(true);
            if (messageCount > 100)
            {
                messageCount = 100;
            }
            var messageList = await ctx.Channel.GetMessagesAsync((int)messageCount);
            await ctx.Channel.DeleteMessagesAsync(messageList);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Selected messages have been pruned"));
        }

        [SlashCommand("AddNote", "Adds a note in the database without warning the user")]
        public async Task AddNote(InteractionContext ctx, [Option("user", "User to who to add the note to")] DiscordUser user, [Option("Note", "Contents of the note.")] string note)
        {
            await ctx.DeferAsync(true);
            await DatabaseContext.AddInfractionsAsync(DatabaseContext, new Infraction(ctx.User.Id, user.Id, ctx.Guild.Id, note, false, "note"));
            
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{ctx.User.Mention}, a note has been added to {user.Username}({user.Id})"));
            
            Guild guild = await DatabaseContext.Guilds.FindAsync(ctx.Guild.Id);
            if (guild != null)
            {
                DiscordChannel channel = ctx.Guild.GetChannel(Convert.ToUInt64(guild.ModerationLogChannelId));
                await CustomMethod.SendModLogAsync(channel, user, $"**Note added to:**\t{user.Mention}\n**by:**\t{ctx.Member.Username}\n**Note:**\t{note}", CustomMethod.ModLogType.Info);
            }
        }

        [SlashCommand("Infractions", "Shows the infractions of the user")]
        public async Task Infractions(InteractionContext ctx, [Option("user", "User to show the infractions for")] DiscordUser user)
        {
            await ctx.DeferAsync();
            DiscordEmbed embed = await WarningService.GetUserWarningsAsync(ctx.Guild, user, true);
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(embed));
        }

        [ContextMenu(ApplicationCommandType.UserContextMenu,"Infractions", false)]
        public async Task InfractionsContextMenu(ContextMenuContext ctx)
        {
            await ctx.DeferAsync(true);
            DiscordEmbed embed = await WarningService.GetUserWarningsAsync(ctx.Guild, ctx.TargetMember, true);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }

        [SlashCommand("FAQ", "Creates a new FAQ message")]
        public async Task Faq(InteractionContext ctx)
        {
            var customId = $"FAQ-{ctx.User.Id}";
            DiscordInteractionResponseBuilder modal = new DiscordInteractionResponseBuilder().WithTitle("New FAQ entry").WithCustomId(customId)
                .AddComponents(new TextInputComponent("Question", "Question", null, null, true, TextInputStyle.Paragraph))
                .AddComponents(new TextInputComponent("Answer", "Answer", "Answer to the question", null, true, TextInputStyle.Paragraph));
            await ctx.CreateResponseAsync(InteractionResponseType.Modal, modal);

            InteractivityExtension interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForModalAsync(customId, ctx.User);
            if (!response.TimedOut)
            {
                await new DiscordMessageBuilder()
                    .WithContent($"**Q: {response.Result.Values["Question"]}**\n *A: {response.Result.Values["Answer"].TrimEnd()}*")
                    .SendAsync(ctx.Channel);
                await response.Result.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("FAQ message created!").AsEphemeral());
            }
        }

        [SlashCommand("FAQ-Edit", "Edits an existing FAQ message, using the message ID")]
        public async Task FaqEdit(InteractionContext ctx, [Option("Message_ID", "The message ID to edit")] string messageId)
        {
            DiscordMessage message = await ctx.Channel.GetMessageAsync(Convert.ToUInt64(messageId));
            string ogMessage = message.Content.Replace("*", string.Empty);
            string question = ogMessage.Substring(ogMessage.IndexOf(":", StringComparison.Ordinal) + 1, ogMessage.Length - (ogMessage[ogMessage.IndexOf("\n", StringComparison.Ordinal)..].Length + 2)).TrimStart();
            string answer = ogMessage[(ogMessage.IndexOf("\n", StringComparison.Ordinal) + 4)..].TrimStart();

            var customId = $"FAQ-Editor-{ctx.User.Id}";
            DiscordInteractionResponseBuilder modal = new DiscordInteractionResponseBuilder().WithTitle("FAQ Editor").WithCustomId(customId)
                .AddComponents(new TextInputComponent("Question", "Question", null, question, true, TextInputStyle.Paragraph))
                .AddComponents(new TextInputComponent("Answer", "Answer", null, answer, true, TextInputStyle.Paragraph));

            await ctx.CreateResponseAsync(InteractionResponseType.Modal, modal);

            InteractivityExtension interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForModalAsync(customId, ctx.User);
            if (!response.TimedOut)
            {
                await message.ModifyAsync($"**Q: {response.Result.Values["Question"]}**\n *A: {response.Result.Values["Answer"].TrimEnd()}*");
                await response.Result.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("FAQ message edited").AsEphemeral());
            }
        }

        [SlashCommand("info", "Shows general info about the user.")]
        public async Task Info(InteractionContext ctx, [Option("User", "User who to get the info about.")] DiscordUser user)
        {
            await ctx.DeferAsync();
            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The user is not in the server, can't find information!"));
                return;
            }

            DiscordEmbed embed = BuildMemberInfoEmbedAsync(member);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }
        [ContextMenu(ApplicationCommandType.UserContextMenu,"Info",false)]
        public async Task InfoContextMenu(ContextMenuContext ctx)
        {
            await ctx.DeferAsync(true);
            DiscordEmbed embed = BuildMemberInfoEmbedAsync(ctx.TargetMember);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }
        private DiscordEmbed BuildMemberInfoEmbedAsync(DiscordMember member)
        {
            DiscordEmbedBuilder embedBuilder = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = member.Username,
                    IconUrl = member.AvatarUrl
                },
                Title = $"{member.Username} Info",
                ImageUrl = member.AvatarUrl
            };
            embedBuilder
                .AddField("Nickname", (member.Nickname ?? "None"), true)
                .AddField("ID", member.Id.ToString(), true)
                .AddField("Account Created On", $"<t:{member.CreationTimestamp.ToUnixTimeSeconds()}:F>")
                .AddField("Server Join Date", $"<t:{member.JoinedAt.ToUnixTimeSeconds()}:F>");
            if (member.IsPending != null)
            {
                bool ispending = member.IsPending.Value;
                embedBuilder.AddField("Accepted rules?", ispending ? "No" : "Yes");
            }
            return embedBuilder.Build();
        }

        [SlashCommand("Message", "Sends a message to specified user. Requires Mod Mail feature enabled.")]
        public async Task Measseg(InteractionContext ctx, [Option("User", "Specify the user who to mention")] DiscordUser user, [Option("Message", "Message to send to the user.")] string message)
        {
            await ctx.DeferAsync(true);
            Guild guildSettings = await DatabaseContext.Guilds.FirstOrDefaultAsync(w => w.Id == ctx.Guild.Id);
            if (guildSettings?.ModMailChannelId == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The Mod Mail feature has not been enabled in this server. Contact an Admin to resolve the issue."));
                return;
            }

            DiscordMember member;
            try
            {
                member = await ctx.Guild.GetMemberAsync(user.Id);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The user is not in the server, can't message."));
                return;
            }
            var dmMessage = $"You are receiving a Moderator DM from **{ctx.Guild.Name}** Discord\n{ctx.User.Username} - {message}";
            DiscordMessageBuilder messageBuilder = new();
            messageBuilder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, $"{ModMailService.OpenButtonPrefix}{ctx.Guild.Id}", "Open Mod Mail"));
            messageBuilder.WithContent(dmMessage);

            await member.SendMessageAsync(messageBuilder);

            DiscordChannel modMailChannel = ctx.Guild.GetChannel(guildSettings.ModMailChannelId.Value);
            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = member.AvatarUrl,
                    Name = member.Username
                },
                Title = $"[MOD DM] Moderator DM to {member.Username}",
                Description = dmMessage
            };
            await modMailChannel.SendMessageAsync(embed: embed);
            ctx.Client.Logger.LogInformation(CustomLogEvents.ModMail, "A Direct message was sent to {Username}({UserId}) from {User2Name}({User2Id}) through Mod Mail system.", member.Username, member.Id, ctx.Member.Username, ctx.Member.Id);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Message delivered to user. Check Mod Mail channel for logs."));
        }

        [ContextMenu(ApplicationCommandType.MessageContextMenu, "Add Button", false)]
        public async Task AddButton(ContextMenuContext ctx)
        {
            if (ctx.TargetMessage.Author != ctx.Client.CurrentUser)
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("To add a button, the bot must be the author of the message. Try again").AsEphemeral());
                return;
            }

            var customId = $"AddButton-{ctx.TargetMessage.Id}-{ctx.User.Id}";
            DiscordInteractionResponseBuilder response = new()
            {
                Title = "Button Parameters",
                CustomId = customId
            };
            response.AddComponents(new TextInputComponent("Custom ID", "customid"));
            response.AddComponents(new TextInputComponent("Label", "label"));
            response.AddComponents(new TextInputComponent("Emoji", "emoji", required: false));

            await ctx.CreateResponseAsync(InteractionResponseType.Modal, response);
            InteractivityExtension interactivity = ctx.Client.GetInteractivity();
            var modalResponse = await interactivity.WaitForModalAsync(customId, ctx.User);

            if (modalResponse.TimedOut) return;

            DiscordMessageBuilder modified = new DiscordMessageBuilder()
                .WithContent(ctx.TargetMessage.Content)
                .AddEmbeds(ctx.TargetMessage.Embeds);

            DiscordComponentEmoji emoji = null;
            if (modalResponse.Result.Values["emoji"] != string.Empty)
            {
                emoji = UInt64.TryParse(modalResponse.Result.Values["emoji"], out ulong emojiId) ? new DiscordComponentEmoji(emojiId) : new DiscordComponentEmoji(modalResponse.Result.Values["emoji"]);
            }

            if (ctx.TargetMessage.Components.Count == 0)
            {
                modified.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, modalResponse.Result.Values["customid"], modalResponse.Result.Values["label"], emoji: emoji));
            }
            foreach (DiscordActionRowComponent row in ctx.TargetMessage.Components)
            {
                if (row.Components.Count == 5)
                {
                    modified.AddComponents(row);
                }
                else
                {
                    var buttons = row.Components.ToList();
                    buttons.Add(new DiscordButtonComponent(ButtonStyle.Primary, modalResponse.Result.Values["customid"], modalResponse.Result.Values["label"], emoji: emoji));
                    modified.AddComponents(buttons);
                }
            }


            await ctx.TargetMessage.ModifyAsync(modified);
            await modalResponse.Result.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Button added to the message. **Custom ID:** {modalResponse.Result.Values["customid"]}").AsEphemeral());
        }
    }
}