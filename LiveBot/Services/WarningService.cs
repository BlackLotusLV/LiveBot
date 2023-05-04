using DSharpPlus.SlashCommands;
using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Services
{
    public interface IWarningService
    {
        public void StartService(DiscordClient client);
        public void StopService();
        public void AddToQueue(WarningItem value);
        public Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId);
        Task<DiscordEmbed> GetInfractionsAsync(DiscordGuild guild, DiscordUser user, bool adminCommand = false);
        Task<DiscordEmbed> GetUserInfoAsync(DiscordGuild guild, DiscordUser user);
        string UserInfoButtonPrefix { get; }
        string InfractionButtonPrefix { get; }
    }

    public class WarningService : BaseQueueService<WarningItem>, IWarningService
    {
        public WarningService(LiveBotDbContext databaseContext) : base(databaseContext)
        {
        }

        private const string _infractionButtonPrefix = "GetInfractions-";
        private const string _userInfoButtonPrefix = "GetUserInfo-";
        public string InfractionButtonPrefix { get; } = _infractionButtonPrefix;
        public string UserInfoButtonPrefix { get; } = _userInfoButtonPrefix;

        private protected override async Task ProcessQueueAsync()
        {
            foreach (WarningItem warningItem in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                try
                {
                    Guild guild = await _databaseContext.Guilds.FindAsync(warningItem.Guild.Id);

                    DiscordMember member = null;
                    try
                    {
                        member = await warningItem.Guild.GetMemberAsync(warningItem.User.Id);
                    }
                    catch (Exception)
                    {
                        if (warningItem.AutoMessage) return;
                        await warningItem.Channel.SendMessageAsync($"{warningItem.User.Username} is no longer in the server.");
                    }

                    var modInfo = "";
                    bool kick = false, ban = false;
                    if (guild?.ModerationLogChannelId == null)
                    {
                        if (warningItem.InteractionContext == null)
                        {
                            await warningItem.Channel.SendMessageAsync("This server has not set up this feature!");
                        }
                        else
                        {
                            await warningItem.InteractionContext.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This server has not set up this feature!"));
                        }

                        return;
                    }

                    DiscordChannel modLog = warningItem.Guild.GetChannel(Convert.ToUInt64(guild.ModerationLogChannelId));

                    Infraction newInfraction = new(warningItem.Admin.Id, warningItem.User.Id, warningItem.Guild.Id, warningItem.Reason, true, InfractionType.Warning);
                    await _databaseContext.AddInfractionsAsync(_databaseContext, newInfraction);

                    int warningCount = await _databaseContext.Infractions.CountAsync(w => w.UserId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.InfractionType == InfractionType.Warning);
                    int infractionLevel = await _databaseContext.Infractions.CountAsync(w => w.UserId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.InfractionType == InfractionType.Warning && w.IsActive);

                    DiscordEmbedBuilder embedToUser = new()
                    {
                        Author = new DiscordEmbedBuilder.EmbedAuthor()
                        {
                            Name = warningItem.Guild.Name,
                            IconUrl = warningItem.Guild.IconUrl
                        },
                        Title = "You have been warned!"
                    };
                    embedToUser.AddField("Reason", warningItem.Reason);
                    embedToUser.AddField("Infraction Level", $"{infractionLevel}", true);
                    embedToUser.AddField("Warning by", $"{warningItem.Admin.Mention}", true);
                    embedToUser.AddField("Server", warningItem.Guild.Name, true);

                    var warningDescription =
                        $"**Warned user:**\t{warningItem.User.Mention}\n**Infraction level:**\t {infractionLevel}\t**Infractions:**\t {warningCount}\n**Warned by**\t{warningItem.Admin.Username}\n**Reason:** {warningItem.Reason}";

                    switch (infractionLevel)
                    {
                        case > 4:
                            embedToUser.AddField("Banned", "Due to you exceeding the Infraction threshold, you have been banned");
                            ban = true;
                            break;
                        case > 2:
                            embedToUser.AddField("Kicked", "Due to you exceeding the Infraction threshold, you have been kicked");
                            kick = true;
                            break;
                    }

                    if (warningItem.AutoMessage)
                    {
                        embedToUser.WithFooter("This message was sent by Auto Moderator, contact staff if you think this is a mistake");
                    }

                    try
                    {
                        if (member != null) await member.SendMessageAsync(embed: embedToUser);
                    }
                    catch
                    {
                        modInfo = $":exclamation:{warningItem.User.Mention} could not be contacted via DM. Reason not sent";
                    }

                    if (kick && member != null)
                    {
                        await member.RemoveAsync("Exceeded warning limit!");
                    }

                    if (ban)
                    {
                        await warningItem.Guild.BanMemberAsync(warningItem.User.Id, 0, "Exceeded warning limit!");
                    }

                    await CustomMethod.SendModLogAsync(modLog, warningItem.User, warningDescription, CustomMethod.ModLogType.Warning, modInfo);

                    if (warningItem.InteractionContext == null)
                    {
                        DiscordMessage info = await warningItem.Channel.SendMessageAsync($"{warningItem.User.Username}, Has been warned!");
                        await Task.Delay(10000);
                        await info.DeleteAsync();
                    }
                    else
                    {
                        await warningItem.InteractionContext.EditResponseAsync(
                            new DiscordWebhookBuilder().WithContent(
                                $"{warningItem.Admin.Mention}, The user {warningItem.User.Mention}({warningItem.User.Id}) has been warned. Please check the log for additional info."));
                        await Task.Delay(10000);
                        await warningItem.InteractionContext.DeleteResponseAsync();
                    }

                }
                catch (Exception e)
                {
                    _client.Logger.LogError("{} failed to process item in queue \n{}", this.GetType().Name,e);
                    continue;
                }
            }
        }

        public async Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId)
        {
            Guild guild = await _databaseContext.Guilds.FindAsync(ctx.Guild.Id);
            var infractions = await _databaseContext.Infractions.Where(w => ctx.Guild.Id == w.GuildId && user.Id == w.UserId && w.InfractionType == InfractionType.Warning && w.IsActive).ToListAsync();
            int infractionLevel = infractions.Count;

            if (infractionLevel == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This user does not have any infractions that are active, did you provide the correct user?"));
                return;
            }

            StringBuilder modMessageBuilder = new();
            DiscordMember member = null;
            if (guild == null || guild.ModerationLogChannelId == null)
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
                modMessageBuilder.AppendLine($"{user.Mention} is no longer in the server.");
            }

            DiscordChannel modLog = ctx.Guild.GetChannel(Convert.ToUInt64(guild.ModerationLogChannelId));
            Infraction entry = infractions.FirstOrDefault(f => f.IsActive && f.Id == warningId);
            entry ??= infractions.Where(f => f.IsActive).OrderBy(f => f.Id).First();
            entry.IsActive = false;

            _databaseContext.Infractions.Update(entry);
            await _databaseContext.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Infraction #{entry.Id} deactivated for {user.Username}({user.Id})"));

            var description = $"{ctx.User.Mention} deactivated infraction #{entry.Id} for user:{user.Mention}. Infraction level: {infractionLevel - 1}";
            try
            {
                if (member != null) await member.SendMessageAsync($"Your infraction level in **{ctx.Guild.Name}** has been lowered to {infractionLevel - 1} by {ctx.User.Mention}");
            }
            catch
            {
                modMessageBuilder.AppendLine($"{user.Mention} could not be contacted via DM.");
            }

            await CustomMethod.SendModLogAsync(modLog, user, description, CustomMethod.ModLogType.Unwarn, modMessageBuilder.ToString());
        }

        public async Task<DiscordEmbed> GetUserInfoAsync(DiscordGuild guild, DiscordUser user)
        {
            DiscordEmbedBuilder embedBuilder = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = user.Username,
                    IconUrl = user.AvatarUrl
                },
                Title = $"{user.Username} Info",
                ImageUrl = user.AvatarUrl,
                Url = $"https://discordapp.com/users/{user.Id}"
            };
            DiscordMember member = null;
            try
            {
                member = await guild.GetMemberAsync(user.Id);
            }
            catch (Exception e)
            {
                _client.Logger.LogError(e, "Failed to get member in GetUserInfoAsync");
            }
            embedBuilder
                .AddField("Nickname", (member is null ? "*User not in this server*" : member.Username??"*None*"), true)
                .AddField("ID", user.Id.ToString(), true)
                .AddField("Account Created On", $"<t:{user.CreationTimestamp.ToUnixTimeSeconds()}:F>")
                .AddField("Server Join Date", (member is null ? "User not in this server" : $"<t:{member.JoinedAt.ToUnixTimeSeconds()}:F>"))
                .AddField("Accepted rules?", (member is null ? "User not in this server" : member.IsPending == null ? "Guild has no member screening" : member.IsPending.Value ? "No" : "Yes"));
            return embedBuilder.Build();
        }

        public async Task<DiscordEmbed> GetInfractionsAsync(DiscordGuild guild, DiscordUser user, bool adminCommand = false)
        {
            var splitCount = 1;
            StringBuilder reason = new();
            GuildUser userStats = await _databaseContext.GuildUsers.FindAsync(new object[] { user.Id, guild.Id }) ?? await _databaseContext.AddGuildUsersAsync(_databaseContext, new GuildUser(user.Id, guild.Id));

            int kickCount = userStats.KickCount;
            int banCount = userStats.BanCount;
            var warningsList = await _databaseContext.Infractions.Where(w => w.UserId == user.Id && w.GuildId == guild.Id).OrderBy(w => w.TimeCreated).ToListAsync();
            if (!adminCommand)
            {
                warningsList.RemoveAll(w => w.InfractionType == InfractionType.Note);
            }

            int infractionLevel = warningsList.Count(w => w.InfractionType == InfractionType.Warning && w.IsActive);
            int infractionCount = warningsList.Count(w => w.InfractionType == InfractionType.Warning);
            foreach (Infraction infraction in warningsList)
            {
                switch (infraction.InfractionType)
                {
                    case InfractionType.Ban:
                        reason.Append("[🔨]");
                        break;

                    case InfractionType.Kick:
                        reason.Append("[🥾]");
                        break;

                    case InfractionType.Note:
                        reason.Append("[❔]");
                        break;

                    case InfractionType.Warning:
                        reason.Append(infraction.IsActive ? "[✅] " : "[❌] ");
                        break;
                }

                var addedInfraction =
                    $"**ID:**{infraction.Id}\t**By:** <@{infraction.AdminDiscordId}>\t**Date:** <t:{(int)(infraction.TimeCreated - new DateTime(1970, 1, 1)).TotalSeconds}>\n**Reason:** {infraction.Reason}\n **Type:**\t{infraction.InfractionType.ToString()}";

                if (reason.Length + addedInfraction.Length > 1023 * splitCount)
                {
                    reason.Append("~split~");
                    splitCount++;
                }

                reason.AppendLine(addedInfraction);
            }

            if (warningsList.Count == 0)
            {
                reason.AppendLine("User has no warnings.");
            }

            DiscordEmbedBuilder embed = new()
            {
                Color = new DiscordColor(0xFF6600),
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{user.Username}({user.Id})",
                    IconUrl = user.AvatarUrl
                },
                Description = $"",
                Title = "Infraction Count",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = user.AvatarUrl
                }
            };
            embed.AddField("Warning level: ", $"{infractionLevel}", true);
            embed.AddField("Times warned: ", $"{infractionCount}", true);
            embed.AddField("Times kicked: ", $"{kickCount}", true);
            embed.AddField("Times banned: ", $"{banCount}", true);
            string[] splitReason = reason.ToString().Split("~split~");
            for (var i = 0; i < splitReason.Length; i++)
            {
                embed.AddField($"Infraction({i + 1}/{splitReason.Length})", splitReason[i]);
            }

            return embed;
        }
    }

    public class WarningItem
    {
        public DiscordUser User { get; set; }
        public DiscordUser Admin { get; set; }
        public DiscordGuild Guild { get; set; }
        public DiscordChannel Channel { get; set; }
        public string Reason { get; set; }
        public bool AutoMessage { get; set; }
        public InteractionContext InteractionContext { get; set; }

        public WarningItem(DiscordUser user, DiscordUser admin, DiscordGuild server, DiscordChannel channel, string reason, bool autoMessage, InteractionContext interactionContext = null)
        {
            User = user;
            Admin = admin;
            Guild = server;
            Channel = channel;
            Reason = reason;
            AutoMessage = autoMessage;
            InteractionContext = interactionContext;
        }
    }
}