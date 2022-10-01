using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Mod", "Moderator commands", false)]
    [SlashRequirePermissions(Permissions.KickMembers)]
    [SlashRequireGuild]
    internal class SlashModeratorCommands : ApplicationCommandModule
    {
        [SlashCommand("warn", "Warn a user.")]
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
            [Autocomplete(typeof(UnwarnOptions))]
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
            entry ??= Warnings.Where(f => f.Active is true).OrderBy(f => f.ID_Warning).FirstOrDefault();
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

            await CustomMethod.SendModLogAsync(modlog, user, Description, CustomMethod.ModLogType.Unwarn, modmsgBuilder.ToString());
        }
        private sealed class UnwarnOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                List<DiscordAutoCompleteChoice> result = new();
                foreach (var item in DB.DBLists.Warnings.Where(w=>w.Server_ID == ctx.Guild.Id && w.User_ID == (ulong)ctx.Options.First(x=>x.Name=="user").Value && w.Type=="warning" && w.Active))
                {

                    result.Add(new DiscordAutoCompleteChoice($"#{item.ID_Warning} - {item.Reason}",(long)item.ID_Warning));
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
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

        [SlashCommand("AddNote", "Adds a note in the database without warning the user")]
        public async Task AddNote(InteractionContext ctx, [Option("user", "User to who to add the note to")] DiscordUser user, [Option("Note", "Contents of the note.")] string note)
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
                await CustomMethod.SendModLogAsync(channel, user, $"**Note added to:**\t{user.Mention}\n**by:**\t{ctx.Member.Username}\n**Note:**\t{note}", CustomMethod.ModLogType.Info);
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

        [ContextMenu(ApplicationCommandType.UserContextMenu,"Infractions")]
        public async Task InfractionsContextMenu(ContextMenuContext ctx)
        {
            await ctx.DeferAsync(true);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(CustomMethod.GetUserWarnings(ctx.Guild, ctx.TargetMember, true)));
        }

        [SlashCommand("FAQ", "Creates a new FAQ message")]
        public async Task FAQ(InteractionContext ctx)
        {
            string customID = $"FAQ-{ctx.User.Id}";
            var modal = new DiscordInteractionResponseBuilder().WithTitle("New FAQ entry").WithCustomId(customID)
                .AddComponents(new TextInputComponent("Question", "Question", null, null, true, TextInputStyle.Paragraph))
                .AddComponents(new TextInputComponent("Answer", "Answer", "Answer to the question", null, true, TextInputStyle.Paragraph));
            await ctx.CreateResponseAsync(InteractionResponseType.Modal, modal);

            var interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForModalAsync(customID, ctx.User);
            if (!response.TimedOut)
            {
                await new DiscordMessageBuilder()
                    .WithContent($"**Q: {response.Result.Values["Question"]}**\n *A: {response.Result.Values["Answer"].TrimEnd()}*")
                    .SendAsync(ctx.Channel);
                await response.Result.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("FAQ message created!").AsEphemeral());
            }
        }

        [SlashCommand("FAQ-Edit", "Edits an existing FAQ message, using the message ID")]
        public async Task FAQEdit(InteractionContext ctx, [Option("Message_ID", "The message ID to edit")] string messageID)
        {
            DiscordMessage message = await ctx.Channel.GetMessageAsync(Convert.ToUInt64(messageID));
            string ogMessage = message.Content.Replace("*", string.Empty);
            string question = ogMessage.Substring(ogMessage.IndexOf(":") + 1, ogMessage.Length - (ogMessage[ogMessage.IndexOf("\n")..].Length + 2)).TrimStart();
            string answer = ogMessage[(ogMessage.IndexOf("\n") + 4)..].TrimStart();

            string customID = $"FAQ-Editor-{ctx.User.Id}";
            var modal = new DiscordInteractionResponseBuilder().WithTitle("FAQ Editor").WithCustomId(customID)
                .AddComponents(new TextInputComponent("Question", "Question", null, question, true, TextInputStyle.Paragraph))
                .AddComponents(new TextInputComponent("Answer", "Answer", null, answer, true, TextInputStyle.Paragraph));

            await ctx.CreateResponseAsync(InteractionResponseType.Modal, modal);

            var interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForModalAsync(customID, ctx.User);
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
        [ContextMenu(ApplicationCommandType.UserContextMenu,"Info")]
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
                bool ispending = member.IsPending ?? false;
                embedBuilder.AddField("Accepted rules?", ispending ? "No" : "Yes");
            }
            return embedBuilder.Build();
        }

        [SlashCommand("Message", "Sends a message to specified user. Requires Mod Mail feature enabled.")]
        public async Task Measseg(InteractionContext ctx, [Option("User", "Specify the user who to mention")] DiscordUser user, [Option("Message", "Message to send to the user.")] string message)
        {
            await ctx.DeferAsync(true);
            DB.ServerSettings guildSettings = DB.DBLists.ServerSettings.FirstOrDefault(w => w.ID_Server == ctx.Guild.Id);
            if (guildSettings?.ModMailID == 0)
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
            string DMMessage = $"You are receiving a Moderator DM from **{ctx.Guild.Name}** Discord\n{ctx.User.Username} - {message}";
            DiscordMessageBuilder messageBuilder = new();
            messageBuilder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, $"openmodmail{ctx.Guild.Id}", "Open Mod Mail"));
            messageBuilder.WithContent(DMMessage);

            await member.SendMessageAsync(messageBuilder);

            DiscordChannel MMChannel = ctx.Guild.GetChannel(guildSettings.ModMailID);
            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = member.AvatarUrl,
                    Name = member.Username
                },
                Title = $"[MOD DM] Moderator DM to {member.Username}",
                Description = DMMessage
            };
            await MMChannel.SendMessageAsync(embed: embed);
            Program.Client.Logger.LogInformation(CustomLogEvents.ModMail, "A Dirrect message was sent to {Username}({UserId}) from {User2Name}({User2Id}) through Mod Mail system.", member.Username, member.Id, ctx.Member.Username, ctx.Member.Id);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Message delivered to user. Check Mod Mail channel for logs."));
        }
    }
}