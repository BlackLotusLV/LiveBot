namespace LiveBot.Automation
{
    internal static class ModMail
    {
        public static readonly int TimeoutMinutes = 120;

        public static async Task ModMailDM(DiscordClient Client, MessageCreateEventArgs e)
        {
            var MMEntry = DB.DBLists.ModMail.FirstOrDefault(w => w.User_ID == e.Author.Id && w.IsActive);
            if (e.Guild == null && MMEntry != null && !(e.Message.Content.StartsWith($"{Program.CFGJson.CommandPrefix}modmail") || e.Message.Content.StartsWith($"{Program.CFGJson.CommandPrefix}mm")))
            {
                DiscordGuild Guild = Client.Guilds.First(w => w.Value.Id == MMEntry.Server_ID).Value;
                DiscordChannel ModMailChannel = Guild.GetChannel(DB.DBLists.ServerSettings.First(w => w.ID_Server == MMEntry.Server_ID).ModMailID);
                DiscordEmbedBuilder embed = new()
                {
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        IconUrl = e.Author.AvatarUrl,
                        Name = $"{e.Author.Username} ({e.Author.Id})"
                    },
                    Color = new DiscordColor(MMEntry.ColorHex),
                    Title = $"[INBOX] #{MMEntry.ID} Mod Mail user message.",
                    Description = e.Message.Content
                };

                if (e.Message.Attachments != null)
                {
                    foreach (var attachment in e.Message.Attachments)
                    {
                        embed.AddField("Atachment", attachment.Url, false);
                    }
                }

                await ModMailChannel.SendMessageAsync(embed: embed);

                MMEntry.HasChatted = true;
                MMEntry.LastMSGTime = DateTime.UtcNow;
                DB.DBLists.UpdateModMail(MMEntry);

                Client.Logger.LogInformation(CustomLogEvents.ModMail, "New Mod Mail message sent to {ChannelName}({ChannelId}) in {GuildName} from {Username}({UserId})", ModMailChannel.Name, ModMailChannel.Id, ModMailChannel.Guild.Name, e.Author.Username, e.Author.Id);
            }
        }

        public static async Task ModMailCloser()
        {
            var TimedOutEntry = DB.DBLists.ModMail.FirstOrDefault(w => w.IsActive && (DateTime.UtcNow - w.LastMSGTime) > TimeSpan.FromMinutes(TimeoutMinutes));
            if (TimedOutEntry != null)
            {
                DiscordUser User = await Program.Client.GetUserAsync(TimedOutEntry.User_ID);
                await CloseModMailAsync(
                    TimedOutEntry,
                    User,
                    "Mod Mail entry auto closed.",
                    "**Mod Mail auto closed!\n----------------------------------------------------**");
            }
        }

        public static async Task CloseModMailAsync(DB.ModMail ModMail, DiscordUser Closer, string ClosingText, string ClosingTextToUser)
        {
            ModMail.IsActive = false;
            string DMNotif = string.Empty;
            DiscordGuild Guild = await Program.Client.GetGuildAsync(ModMail.Server_ID);
            DiscordChannel ModMailChannel = Guild.GetChannel(DB.DBLists.ServerSettings.First(w => w.ID_Server == Guild.Id).ModMailID);
            DiscordEmbedBuilder embed = new()
            {
                Title = $"[CLOSED] #{ModMail.ID} {ClosingText}",
                Color = new DiscordColor(ModMail.ColorHex),
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{Closer.Username} ({Closer.Id})",
                    IconUrl = Closer.AvatarUrl
                },
            };
            try
            {
                DiscordMember member = await Guild.GetMemberAsync(ModMail.User_ID);
                await member.SendMessageAsync(ClosingTextToUser);
            }
            catch
            {
                DMNotif = "User could not be contacted anymore, either blocked the bot, left the server or turned off DMs";
            }
            DB.DBLists.UpdateModMail(ModMail);
            await ModMailChannel.SendMessageAsync(DMNotif, embed: embed);
        }

        public static async Task ModMailCloseButton(object Client, ComponentInteractionCreateEventArgs e)
        {
            if (e.Interaction.Type != InteractionType.Component || e.Interaction.User.IsBot || !e.Interaction.Data.CustomId.Contains("close")) return;
            var MMEntry = DB.DBLists.ModMail.FirstOrDefault(w => w.IsActive && $"{w.ID}" == e.Interaction.Data.CustomId.Replace("close", ""));
            DiscordInteractionResponseBuilder discordInteractionResponseBuilder = new();
            if (e.Message.Embeds.Count>0)
            {
                discordInteractionResponseBuilder.AddEmbeds(e.Message.Embeds);
            }
            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, discordInteractionResponseBuilder.WithContent(e.Message.Content));
            if (MMEntry == null) return;
            await CloseModMailAsync(
                MMEntry,
                e.Interaction.User,
                $" Mod Mail closed by {e.Interaction.User.Username}",
                $"**Mod Mail closed by {e.Interaction.User.Username}!\n----------------------------------------------------**");
        }
        public static async Task ModMailDMOpenButton(DiscordClient Client, ComponentInteractionCreateEventArgs e)
        {
            if (e.Interaction.Type != InteractionType.Component || e.Interaction.User.IsBot || !e.Interaction.Data.CustomId.Contains("openmodmail")) return;
            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            DiscordGuild guild = await Client.GetGuildAsync(Convert.ToUInt64(e.Interaction.Data.CustomId.Replace("openmodmail","")));
            if (DB.DBLists.ServerRanks.First(w=>w.Server_ID == guild.Id && w.User_ID == e.User.Id).MM_Blocked)
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("You are blocked from using the Mod Mail feature in this server."));
                return;
            }
            if (DB.DBLists.ModMail.Any(w => w.User_ID == e.User.Id && w.IsActive))
            {
                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("You already have an existing Mod Mail open, please close it before starting a new one."));
                return;
            }

            Random r = new();
            string colorID = string.Format("#{0:X6}", r.Next(0x1000000));
            DB.ModMail newEntry = new()
            {
                Server_ID = guild.Id,
                User_ID = e.User.Id,
                LastMSGTime = DateTime.UtcNow,
                ColorHex = colorID,
                IsActive = true,
                HasChatted = false
            };

            long EntryID = DB.DBLists.InsertModMail(newEntry);
            DiscordButtonComponent CloseButton = new(ButtonStyle.Danger, $"close{EntryID}", "Close", false, new DiscordComponentEmoji("✖️"));

            await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().AddComponents(CloseButton).WithContent($"**----------------------------------------------------**\n" +
                            $"Modmail entry **open** with `{guild.Name}`. Continue to write as you would normally ;)\n*Mod Mail will time out in {TimeoutMinutes} minutes after last message is sent.*\n" +
                            $"**Subject: No subject, Mod Mail Opened with button**"));

            DiscordEmbedBuilder embed = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{e.User.Username} ({e.User.Id})",
                    IconUrl = e.User.AvatarUrl
                },
                Title = $"[NEW] #{EntryID} Mod Mail created by {e.User.Username}.",
                Color = new DiscordColor(colorID),
                Description = "No subject, Mod Mail Opened with button"
            };

            DiscordChannel modMailChannel = guild.GetChannel(DB.DBLists.ServerSettings.First(w=>w.ID_Server== guild.Id).ModMailID);
            await new DiscordMessageBuilder()
                .AddComponents(CloseButton)
                .WithEmbed(embed)
                .SendAsync(modMailChannel);

        }
    }
}