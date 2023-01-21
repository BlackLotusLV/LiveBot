using System.Collections.Concurrent;
using LiveBot.DB;

namespace LiveBot.Services
{
    public interface IStreamNotificationService
    {
        void StartService(DiscordClient client);
        void StopService(DiscordClient client);
        void QueueStream(StreamNotifications streamNotification, PresenceUpdateEventArgs e, DiscordGuild guild, DiscordChannel channel, LiveStreamer streamer);
    }
    public class StreamNotificationService : IStreamNotificationService
    {
        public static List<LiveStreamer> LiveStreamerList { get; set; } = new();
        public static int StreamCheckDelay { get; } = 5;

        private static readonly ConcurrentQueue<StreamNotificationItem> Notifications = new();
        // Use a CancellationTokenSource and CancellationToken to be able to stop the thread
        private static readonly CancellationTokenSource Cts = new();
        private static readonly CancellationToken Token = Cts.Token;
        
        private static readonly Thread NotificationThread = new(async () =>
        {
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    if (Notifications.IsEmpty)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (Notifications.TryDequeue(out StreamNotificationItem item))
                    {
                        await StreamNotificationAsync(item.StreamNotification, item.EventArgs, item.Guild, item.Channel, item.Streamer);
                    }
                }
                catch (Exception ex)
                {
                    Program.Client.Logger.LogError(CustomLogEvents.LiveBot, "Stream Notification Service experienced an error\n{exceptionMessage}", ex.Message);
                }
            }
        });
        
        private Timer StreamDelayTimer { get; } = new(e => StreamListCheck());

        public void StartService(DiscordClient client)
        {
            client.Logger.LogInformation(CustomLogEvents.LiveBot,"Stream notification service starting.");
            NotificationThread.Start();
            StreamDelayTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(2));
            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Stream notification service started.");
        }
        public void StopService(DiscordClient client)
        {
            // Request cancellation of the thread
            Cts.Cancel();

            // Wait for the thread to finish
            NotificationThread.Join();

            client.Logger.LogInformation(CustomLogEvents.LiveBot, "Leaderboard service stopped.");
        }

        public void QueueStream(StreamNotifications streamNotification, PresenceUpdateEventArgs e, DiscordGuild guild, DiscordChannel channel, LiveStreamer streamer)
        {
            Notifications.Enqueue(new(streamNotification,e,guild,channel,streamer));
        }
        private static async Task StreamNotificationAsync(StreamNotifications streamNotification, PresenceUpdateEventArgs e, DiscordGuild guild, DiscordChannel channel, LiveStreamer streamer)
        {
            DiscordMember streamMember = await guild.GetMemberAsync(e.User.Id);
            if (e.User==null || e.User.Presence ==null || e.User.Presence.Activities == null) return;
            DiscordActivity activity = e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube");
            if (activity == null || activity.RichPresence?.State == null || activity.RichPresence?.Details == null || activity.StreamUrl == null) return;
            string gameTitle = activity.RichPresence.State;
            string streamTitle = activity.RichPresence.Details;
            string streamUrl = activity.StreamUrl;

            var roleIds = new HashSet<ulong>(streamNotification.RoleIds ?? Array.Empty<ulong>());
            var games = new HashSet<string>(streamNotification.Games ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            bool role = roleIds.Count == 0 || streamMember.Roles.Any(r => roleIds.Contains(r.Id));
            bool game = games.Count == 0 || games.Contains(gameTitle);

            if (!game || !role) return;
            string description = $"**Streamer:**\n {e.User.Mention}\n\n" +
                 $"**Game:**\n{gameTitle}\n\n" +
                 $"**Stream title:**\n{streamTitle}\n\n" +
                 $"**Stream Link:**\n{streamUrl}";
            DiscordEmbedBuilder embed = new()
            {
                Color = new DiscordColor(0x6441A5),
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = e.User.AvatarUrl,
                    Name = "STREAM",
                    Url = streamUrl
                },
                Description = description,
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = e.User.AvatarUrl
                },
                Title = $"Check out {e.User.Username} is now Streaming!"
            };
            await channel.SendMessageAsync(embed: embed);
            //adds user to list
            LiveStreamerList.Add(streamer);
        }

        private static void StreamListCheck()
        {
            try
            {
                foreach (LiveStreamer item in LiveStreamerList.Where(item => item.Time.AddHours(StreamCheckDelay) < DateTime.UtcNow && item.User.Presence.Activity.ActivityType != ActivityType.Streaming))
                {
                    Program.Client.Logger.LogDebug(CustomLogEvents.LiveStream, "User {UserName} removed from Live Stream List - {CheckDelay} hours passed.", item.User.Username, StreamCheckDelay);
                    LiveStreamerList.Remove(item);
                }
            }
            catch (Exception)
            {
                Program.Client.Logger.LogDebug(CustomLogEvents.LiveStream, "Live Stream list is empty. No-one to remove or check.");
            }
        }
    }


    internal class StreamNotificationItem
    {
        public StreamNotifications StreamNotification { get; set; }
        public PresenceUpdateEventArgs EventArgs { get; set; }
        public DiscordGuild Guild { get; set; }
        public DiscordChannel Channel { get; set; }
        public LiveStreamer Streamer { get; set; }
        public StreamNotificationItem(StreamNotifications streamNotification, PresenceUpdateEventArgs eventArgs, DiscordGuild guild, DiscordChannel channel, LiveStreamer streamer)
        {
            this.StreamNotification = streamNotification;
            this.EventArgs = eventArgs;
            this.Guild = guild;
            this.Channel = channel;
            this.Streamer = streamer;
        }
    }
    public class LiveStreamer
    {
        public DiscordUser User { get; init; }
        public DateTime Time { get; init; }
        public DiscordGuild Guild { get; init; }
        public DiscordChannel Channel { get; init; }
    }
}
