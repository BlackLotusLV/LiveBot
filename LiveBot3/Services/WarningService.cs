using DSharpPlus.SlashCommands;
using System.Collections.Concurrent;
using LiveBot.DB;

namespace LiveBot.Services
{
    public interface IWarningService
    {
        public void StartService(DiscordClient client);
        public void StopService(DiscordClient client);
        public void QueueWarning(DiscordUser user, DiscordUser admin, DiscordGuild server, DiscordChannel channel, string reason, bool autoMessage, InteractionContext ctx = null);
        public Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId);
    }

    public class WarningService : IWarningService
    {
        private static readonly ConcurrentQueue<WarningItem> Warnings = new();

        // Use a CancellationTokenSource and CancellationToken to be able to stop the thread
        private static readonly CancellationTokenSource Cts = new();
        private static readonly CancellationToken Token = Cts.Token;

        private static readonly Thread WarningThread = new(Start);

        private static async void Start()
        {
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    if (Warnings.IsEmpty)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (!Warnings.TryDequeue(out WarningItem item)) continue;
                    await WarnUserAsync(item.User, item.Admin, item.Guild, item.Channel, item.Reason, item.AutoMessage, item.Ctx);
                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    Program.Client.Logger.LogError(CustomLogEvents.LiveBot, "Warning Service experienced an error\n{exceptionMessage}", ex.Message);
                }
            }
        }

        public void StartService(DiscordClient client)
        {
            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Starting Warning service.");
            WarningThread.Start();
            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Warning Service started.");
        }

        public void StopService(DiscordClient client)
        {
            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Stopping Warning service.");
            // Request cancellation of the thread
            Cts.Cancel();

            // Wait for the thread to finish
            WarningThread.Join();

            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Warning service stopped!");
        }

        public void QueueWarning(DiscordUser user, DiscordUser admin, DiscordGuild server, DiscordChannel channel, string reason, bool autoMessage, InteractionContext ctx = null)
        {
            Warnings.Enqueue(new WarningItem(user, admin, server, channel, reason, autoMessage, ctx));
        }

        public async Task RemoveWarningAsync(DiscordUser user, InteractionContext ctx, int warningId)
        {
            ServerSettings serverSettings = DBLists.ServerSettings.FirstOrDefault(f => ctx.Guild.Id == f.ID_Server);
            List<Warnings> infractions = DBLists.Warnings.Where(w => ctx.Guild.Id == w.Server_ID && user.Id == w.User_ID && w.Type == "warning" && w.Active).ToList();
            int infractionLevel = infractions.Count;

            if (infractionLevel == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This user does not have any infractions that are active, did you provide the correct user?"));
                return;
            }

            StringBuilder modMessageBuilder = new();
            DiscordMember member = null;
            if (serverSettings == null || serverSettings.WKB_Log == 0)
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

            DiscordChannel modLog = ctx.Guild.GetChannel(Convert.ToUInt64(serverSettings.WKB_Log));
            Warnings entry = infractions.FirstOrDefault(f => f.Active && f.ID_Warning == warningId);
            entry ??= infractions.Where(f => f.Active).OrderBy(f => f.ID_Warning).First();
            entry.Active = false;
            DBLists.UpdateWarnings(entry);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Infraction #{entry.ID_Warning} deactivated for {user.Username}({user.Id})"));

            string description = $"{ctx.User.Mention} deactivated infraction #{entry.ID_Warning} for user:{user.Mention}. Infraction level: {infractionLevel - 1}";
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

        private static async Task WarnUserAsync(DiscordUser user, DiscordUser admin, DiscordGuild server, DiscordChannel channel, string reason, bool autoMessage, InteractionContext ctx = null)
        {
            ServerSettings serverSettings = DBLists.ServerSettings.FirstOrDefault(f => server.Id == f.ID_Server);

            DiscordMember member = null;
            try
            {
                member = await server.GetMemberAsync(user.Id);
            }
            catch (Exception)
            {
                if (autoMessage) return;
                await channel.SendMessageAsync($"{user.Username} is no longer in the server.");
            }

            string modInfo = "";
            bool kick = false, ban = false;
            if (serverSettings == null || serverSettings.WKB_Log == 0)
            {
                if (ctx == null)
                {
                    await channel.SendMessageAsync("This server has not set up this feature!");
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("This server has not set up this feature!"));
                }

                return;
            }

            DiscordChannel modLog = server.GetChannel(Convert.ToUInt64(serverSettings.WKB_Log));

            Warnings newWarning = new()
            {
                Reason = reason,
                Active = true,
                Time_Created = DateTime.UtcNow,
                Admin_ID = admin.Id,
                User_ID = user.Id,
                Server_ID = server.Id,
                Type = "warning"
            };
            DBLists.InsertWarnings(newWarning);

            int warningCount = DBLists.Warnings.Count(w => w.User_ID == user.Id && w.Server_ID == server.Id && w.Type == "warning");
            int infractionLevel = DBLists.Warnings.Count(w => w.User_ID == user.Id && w.Server_ID == server.Id && w.Type == "warning" && w.Active);

            DiscordEmbedBuilder embedToUser = new()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor()
                {
                    Name = server.Name,
                    IconUrl = server.IconUrl
                },
                Title = "You have been warned!"
            };
            embedToUser.AddField("Reason", reason);
            embedToUser.AddField("Infraction Level", $"{infractionLevel}", true);
            embedToUser.AddField("Warning by", $"{admin.Mention}",true);
            embedToUser.AddField("Server", server.Name, true);
            
            string warningDescription =
                $"**Warned user:**\t{user.Mention}\n**Infraction level:**\t {infractionLevel}\t**Infractions:**\t {warningCount}\n**Warned by**\t{admin.Username}\n**Reason:** {reason}";

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

            if (autoMessage)
            {
                embedToUser.WithFooter("This message was sent by Auto Moderator, contact staff if you think this is a mistake");
            }
            try
            {
                if (member != null) await member.SendMessageAsync(embed: embedToUser);
            }
            catch
            {
                modInfo = $":exclamation:{user.Mention} could not be contacted via DM. Reason not sent";
            }

            if (kick && member != null)
            {
                await member.RemoveAsync("Exceeded warning limit!");
            }

            if (ban)
            {
                await server.BanMemberAsync(user.Id, 0, "Exceeded warning limit!");
            }

            await CustomMethod.SendModLogAsync(modLog, user, warningDescription, CustomMethod.ModLogType.Warning, modInfo);

            if (ctx == null)
            {
                DiscordMessage info = await channel.SendMessageAsync($"{user.Username}, Has been warned!");
                await Task.Delay(10000);
                await info.DeleteAsync();
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{admin.Mention}, The user {user.Mention}({user.Id}) has been warned. Please check the log for additional info."));
                await Task.Delay(10000);
                await ctx.DeleteResponseAsync();
            }
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
        public InteractionContext Ctx { get; set; }

        public WarningItem(DiscordUser user, DiscordUser admin, DiscordGuild server, DiscordChannel channel, string reason, bool autoMessage, InteractionContext ctx = null)
        {
            User = user;
            Admin = admin;
            Guild = server;
            Channel = channel;
            Reason = reason;
            AutoMessage = autoMessage;
            this.Ctx = ctx;
        }
    }
}