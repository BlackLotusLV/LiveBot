using DSharpPlus.SlashCommands;
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
    }

    public class WarningService : BaseQueueService<WarningItem>, IWarningService
    {
        public WarningService(ILogger<WarningService> logger, LiveBotDbContext databaseContext) : base(logger, databaseContext) { }

        private protected override async Task ProcessQueueAsync()
        {
            foreach (WarningItem warningItem in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                ServerSettings serverSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(x => x.GuildId == warningItem.Guild.Id);

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
                if (serverSettings == null || serverSettings.ModerationLogChannelId == 0)
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

                DiscordChannel modLog = warningItem.Guild.GetChannel(Convert.ToUInt64(serverSettings.ModerationLogChannelId));

                Warnings newWarning = new(_databaseContext, warningItem.Admin.Id, warningItem.User.Id, warningItem.Guild.Id, warningItem.Reason, true, "warning");
                await _databaseContext.AddAsync(newWarning);
                await _databaseContext.SaveChangesAsync();

                int warningCount = await _databaseContext.Warnings.CountAsync(w => w.UserDiscordId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.Type == "warning");
                int infractionLevel = await _databaseContext.Warnings.CountAsync(w => w.UserDiscordId == warningItem.User.Id && w.GuildId == warningItem.Guild.Id && w.Type == "warning" && w.IsActive);

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
        ServerSettings serverSettings = await _databaseContext.ServerSettings.FirstOrDefaultAsync(f => ctx.Guild.Id == f.GuildId);
        var infractions = await _databaseContext.Warnings.Where(w => ctx.Guild.Id == w.GuildId && user.Id == w.UserDiscordId && w.Type == "warning" && w.IsActive).ToListAsync();
        int infractionLevel = infractions.Count;

        if (infractionLevel == 0)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This user does not have any infractions that are active, did you provide the correct user?"));
            return;
        }

        StringBuilder modMessageBuilder = new();
        DiscordMember member = null;
        if (serverSettings == null || serverSettings.ModerationLogChannelId == 0)
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

        DiscordChannel modLog = ctx.Guild.GetChannel(Convert.ToUInt64(serverSettings.ModerationLogChannelId));
        Warnings entry = infractions.FirstOrDefault(f => f.IsActive && f.IdWarning == warningId);
        entry ??= infractions.Where(f => f.IsActive).OrderBy(f => f.IdWarning).First();
        entry.IsActive = false;

        _databaseContext.Warnings.Update(entry);
        await _databaseContext.SaveChangesAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Infraction #{entry.IdWarning} deactivated for {user.Username}({user.Id})"));

        var description = $"{ctx.User.Mention} deactivated infraction #{entry.IdWarning} for user:{user.Mention}. Infraction level: {infractionLevel - 1}";
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