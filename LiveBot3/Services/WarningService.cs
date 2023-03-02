﻿using DSharpPlus.SlashCommands;
using System.Collections.Concurrent;
using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Services
{
    public interface IWarningService
    {
        public void StartService();
        public void StopService();
        public void AddToQueue(WarningItem value);
        public Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId);
        Task<DiscordEmbed> GetUserWarningsAsync(DiscordGuild Guild, DiscordUser User, bool AdminCommand = false);
    }

    public class WarningService : BaseQueueService<WarningItem>, IWarningService
    {
        public WarningService(LiveBotDbContext databaseContext) : base(databaseContext)
        {
        }

        private protected override async Task ProcessQueueAsync()
        {
            foreach (WarningItem warningItem in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                Guild guild = await _databaseContext.Guilds.FirstOrDefaultAsync(x => x.Id == warningItem.Guild.Id);

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
                if (guild == null || guild.ModerationLogChannelId == null)
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

                //Infraction newInfraction = new(_databaseContext, warningItem.Admin.Id, warningItem.User.Id, warningItem.Guild.Id, warningItem.Reason, true, "warning");
                //await _databaseContext.AddAsync(newInfraction);
                await _databaseContext.SaveChangesAsync();

                int warningCount = await _databaseContext.Warnings.CountAsync(w => w.UserId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.Type == "warning");
                int infractionLevel = await _databaseContext.Warnings.CountAsync(w => w.UserId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.Type == "warning" && w.IsActive);

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
        }

        public async Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId)
        {
            Guild guild = await _databaseContext.Guilds.FirstOrDefaultAsync(f => ctx.Guild.Id == f.Id);
            var infractions = await _databaseContext.Warnings.Where(w => ctx.Guild.Id == w.GuildId && user.Id == w.UserId && w.Type == "warning" && w.IsActive).ToListAsync();
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

            _databaseContext.Warnings.Update(entry);
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
        public async Task<DiscordEmbed> GetUserWarningsAsync(DiscordGuild Guild, DiscordUser User, bool AdminCommand = false)
        {
            int kcount = 0,
                bcount = 0,
                wlevel = 0,
                wcount = 0,
                splitcount = 1;
            StringBuilder Reason = new();
            var UserStats = await _databaseContext.GuildUsers.FirstOrDefaultAsync(f => User.Id == f.UserDiscordId && Guild.Id == f.GuildId);
            if (UserStats == null)
            {
                await _databaseContext.GuildUsers.AddAsync(new GuildUser(_databaseContext, User.Id, Guild.Id));
                await _databaseContext.SaveChangesAsync();
                UserStats = await _databaseContext.GuildUsers.FirstOrDefaultAsync(f => User.Id == f.UserDiscordId && Guild.Id == f.GuildId);
            }
            kcount = UserStats.KickCount;
            bcount = UserStats.BanCount;
            var WarningsList = await _databaseContext.Warnings.Where(w => w.UserId == User.Id && w.GuildId == Guild.Id).OrderBy(w => w.TimeCreated).ToListAsync();
            if (!AdminCommand)
            {
                WarningsList.RemoveAll(w => w.Type == "note");
            }
            wlevel = WarningsList.Count(w => w.Type == "warning" && w.IsActive);
            wcount = WarningsList.Count(w => w.Type == "warning");
            foreach (var item in WarningsList)
            {
                switch (item.Type)
                {
                    case "ban":
                        Reason.Append("[🔨]");
                        break;

                    case "kick":
                        Reason.Append("[🥾]");
                        break;

                    case "note":
                        Reason.Append("[❔]");
                        break;

                    default: // warning
                        if (item.IsActive)
                        {
                            Reason.Append("[✅] ");
                        }
                        else
                        {
                            Reason.Append("[❌] ");
                        }
                        break;
                }
                string addedInfraction = $"**ID:**{item.Id}\t**By:** <@{item.AdminDiscordId}>\t**Date:** <t:{(int)(item.TimeCreated - new DateTime(1970, 1, 1)).TotalSeconds}>\n**Reason:** {item.Reason}\n **Type:**\t{item.Type}";

                if (Reason.Length + addedInfraction.Length > 1023 * splitcount)
                {
                    Reason.Append("~split~");
                    splitcount++;
                }
                Reason.AppendLine(addedInfraction);
            }
            if (WarningsList.Count == 0)
            {
                Reason.AppendLine("User has no warnings.");
            }
            DiscordEmbedBuilder embed = new()
            {
                Color = new DiscordColor(0xFF6600),
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{User.Username}({User.Id})",
                    IconUrl = User.AvatarUrl
                },
                Description = $"",
                Title = "Infraction Count",
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = User.AvatarUrl
                }
            };
            embed.AddField("Warning level: ", $"{wlevel}", true);
            embed.AddField("Times warned: ", $"{wcount}", true);
            embed.AddField("Times kicked: ", $"{kcount}", true);
            embed.AddField("Times banned: ", $"{bcount}", true);
            string[] SplitReason = Reason.ToString().Split("~split~");
            for (int i = 0; i < SplitReason.Length; i++)
            {
                embed.AddField($"Infraction({i + 1}/{SplitReason.Length})", SplitReason[i], false);
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