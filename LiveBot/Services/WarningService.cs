﻿using DSharpPlus.SlashCommands;
using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Services;

public interface IWarningService
{
    public void StartService(DiscordClient client);
    public void StopService();
    public void AddToQueue(WarningItem value);
    public Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId);
    Task<DiscordEmbed> GetUserInfoAsync(DiscordGuild guild, DiscordUser user);
    Task<List<DiscordEmbed>> BuildInfractionsEmbedsAsync(DiscordGuild guild, DiscordUser user, bool adminCommand = false);
    string UserInfoButtonPrefix { get; }
    string InfractionButtonPrefix { get; }
}

public class WarningService : BaseQueueService<WarningItem>, IWarningService
    {
        private readonly IModLogService _modLogService;
        public WarningService(IDbContextFactory<LiveBotDbContext> dbContextFactory,IDatabaseMethodService databaseMethodService, IModLogService modLogService, ILoggerFactory loggerFactory) : base(dbContextFactory, databaseMethodService,loggerFactory)
        {
            _modLogService = modLogService;
        }

        private const string _infractionButtonPrefix = "GetInfractions-";
        private const string _userInfoButtonPrefix = "GetUserInfo-";
        public string InfractionButtonPrefix { get; } = _infractionButtonPrefix;
        public string UserInfoButtonPrefix { get; } = _userInfoButtonPrefix;
        

        private protected override async Task ProcessQueueAsync()
        {
            foreach (WarningItem warningItem in Queue.GetConsumingEnumerable(CancellationTokenSource.Token))
            {
                
                await using LiveBotDbContext liveBotDbContext = await DbContextFactory.CreateDbContextAsync();
                try
                {
                    Guild guild = await liveBotDbContext.Guilds.FindAsync(warningItem.Guild.Id);

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
                    await DatabaseMethodService.AddInfractionsAsync(newInfraction);

                    int warningCount = await liveBotDbContext.Infractions.CountAsync(w => w.UserId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.InfractionType == InfractionType.Warning);
                    int infractionLevel = await liveBotDbContext.Infractions.CountAsync(w => w.UserId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.InfractionType == InfractionType.Warning && w.IsActive);
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

                    string warningDescription = "# User Warned\n" +
                                                $"- **User:** {warningItem.User.Mention}\n" +
                                                $"- **Infraction level:** {infractionLevel}\n" +
                                                $"- **Infractions:** {warningCount}\n" +
                                                $"- **Moderator:** {warningItem.Admin.Mention}\n" +
                                                $"- **Reason:** {warningItem.Reason}\n" +
                                                $"*Infraction ID: {newInfraction.Id}*";

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

                    _modLogService.AddToQueue(new ModLogItem(modLog, warningItem.User, warningDescription, ModLogType.Warning, modInfo,warningItem.Attachment));

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
                    Logger.LogError(CustomLogEvents.ServiceError,e,"{} failed to process item in queue", GetType().Name);
                }
            }
        }

        public async Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId)
        {
            await using LiveBotDbContext liveBotDbContext = await DbContextFactory.CreateDbContextAsync();
            Guild guild = await liveBotDbContext.Guilds.FindAsync(ctx.Guild.Id);
            var infractions = await liveBotDbContext.Infractions.Where(w => ctx.Guild.Id == w.GuildId && user.Id == w.UserId && w.InfractionType == InfractionType.Warning && w.IsActive).ToListAsync();
            int infractionLevel = infractions.Count;

            if (infractionLevel == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This user does not have any infractions that are active, did you provide the correct user?"));
                return;
            }

            StringBuilder modMessageBuilder = new();
            DiscordMember member = null;
            if (guild?.ModerationLogChannelId == null)
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

            liveBotDbContext.Infractions.Update(entry);
            await liveBotDbContext.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Infraction #{entry.Id} deactivated for {user.Username}({user.Id})"));
            
            string description = $"# User Warning Removed\n" +
                                 $"- **User:** {user.Mention}\n" +
                                 $"- **Infraction ID:** {entry.Id}\n" +
                                 $"- **Infraction level:** {infractionLevel - 1}\n" +
                                 $"- **Moderator:** {ctx.User.Mention}\n";
            try
            {
                if (member != null) await member.SendMessageAsync($"Your infraction level in **{ctx.Guild.Name}** has been lowered to {infractionLevel - 1} by {ctx.User.Mention}");
            }
            catch
            {
                modMessageBuilder.AppendLine($"{user.Mention} could not be contacted via DM.");
            }

            _modLogService.AddToQueue(new ModLogItem(modLog, user, description, ModLogType.UnWarn, modMessageBuilder.ToString()));
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
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                Logger.LogDebug("Moderator tried to get info on user {User} in {Guild} but they are not in the server", user.Username, guild.Name);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to get member in GetUserInfoAsync");
            }
            embedBuilder
                .AddField("Nickname", member is null ? "*User not in this server*" : member.Username??"*None*", true)
                .AddField("ID", user.Id.ToString(), true)
                .AddField("Account Created On", $"<t:{user.CreationTimestamp.ToUnixTimeSeconds()}:F>")
                .AddField("Server Join Date", member is null ? "User not in this server" : $"<t:{member.JoinedAt.ToUnixTimeSeconds()}:F>")
                .AddField("Accepted rules?", member is null ? "User not in this server" : member.IsPending == null ? "Guild has no member screening" : member.IsPending.Value ? "No" : "Yes");
            return embedBuilder.Build();
        }

        public async Task<List<DiscordEmbed>> BuildInfractionsEmbedsAsync(DiscordGuild guild, DiscordUser user, bool adminCommand = false)
        {
            await using LiveBotDbContext liveBotDbContext = await DbContextFactory.CreateDbContextAsync();
            GuildUser userStats = await liveBotDbContext.GuildUsers.FindAsync(user.Id, guild.Id) ?? await DatabaseMethodService.AddGuildUsersAsync(new GuildUser(user.Id, guild.Id));
            int kickCount = userStats.KickCount;
            int banCount = userStats.BanCount;
            var userInfractions = await liveBotDbContext.Infractions.Where(w => w.UserId == user.Id && w.GuildId == guild.Id).OrderBy(w => w.TimeCreated).ToListAsync();
            if (!adminCommand)
            {
                userInfractions.RemoveAll(w => w.InfractionType == InfractionType.Note);
            }
            DiscordEmbedBuilder statsEmbed = new()
            {
                Color = new DiscordColor(0xFF6600),
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{user.Username}({user.Id})",
                    IconUrl = user.AvatarUrl
                },
                Description = $"- **Times warned:** {userInfractions.Count(w => w.InfractionType == InfractionType.Warning)}\n" +
                              $"- **Times kicked:** {kickCount}\n" +
                              $"- **Times banned:** {banCount}\n" +
                              $"- **Infraction level:** {userInfractions.Count(w => w.IsActive)}\n" +
                              $"- **Infraction count:** {userInfractions.Count(w => w.IsActive)}\n" +
                              $"- **Mod Mail blocked:** {(userStats.IsModMailBlocked?"Yes":"No")}",
                Title = "Infraction History",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = user.AvatarUrl,
                }
            };
            const int pageCap = 6;
            DiscordEmbedBuilder infractionEmbed = new()
            {
                Color = new DiscordColor(0xFF6600)
            };
            List<DiscordEmbed> embeds = new() { statsEmbed.Build() };

            var infractions = new string[(int)Math.Ceiling((double)userInfractions.Count / pageCap)];
            for (var i = 0; i < userInfractions.Count; i+=pageCap)
            {
                StringBuilder reason = new();
                for (int j = i; j < i + pageCap && j < userInfractions.Count; j++)
                {
                    Infraction infraction = userInfractions[j];
                    reason.AppendLine($"### {GetReasonTypeEmote(infraction.InfractionType)}Infraction #{infraction.Id} *({infraction.InfractionType.ToString()})*\n" +
                                      $"- **By:** <@{infraction.AdminDiscordId}>\n" +
                                      $"- **Date:** <t:{infraction.TimeCreated.ToUnixTimeSeconds()}>\n" +
                                      $"- **Reason:** {infraction.Reason}");
                    if (infraction.InfractionType == InfractionType.Warning)
                    {
                        reason.AppendLine($"- **Is active:** {(infraction.IsActive ? "✅" : "❌")}");
                    }
                }
                infractions[i/pageCap]=reason.ToString();
                infractionEmbed
                    .WithDescription(reason.ToString())
                    .WithTitle($"Infraction History ({i/pageCap+1}/{infractions.Length})");
                embeds.Add(infractionEmbed.Build());
            }

            return embeds;
        }
        private static string GetReasonTypeEmote(InfractionType infractionType)
        {
            return infractionType switch
            {
                InfractionType.Ban => "[🔨]",
                InfractionType.Kick => "[👢]",
                InfractionType.Note => "[📝]",
                InfractionType.Warning => "[⚠️]",
                InfractionType.TimeoutAdded => "[⏳]",
                InfractionType.TimeoutRemoved => "[⌛]",
                InfractionType.TimeoutReduced => "[⏳]",
                InfractionType.TimeoutExtended => "[⏳]",
                _ => "❓"
            };
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
    public DiscordAttachment Attachment { get; set; }

    public WarningItem(DiscordUser user, DiscordUser admin, DiscordGuild server, DiscordChannel channel, string reason, bool autoMessage, InteractionContext interactionContext = null,
        DiscordAttachment attachment = null)
    {
        User = user;
        Admin = admin;
        Guild = server;
        Channel = channel;
        Reason = reason;
        AutoMessage = autoMessage;
        InteractionContext = interactionContext;
        Attachment = attachment;
    }
}