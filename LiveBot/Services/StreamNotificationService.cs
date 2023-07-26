using System.Collections.Concurrent;
using System.Threading.Channels;
using LiveBot.DB;

namespace LiveBot.Services
{
    public interface IStreamNotificationService
    {
        void StartService(DiscordClient client);
        void StopService();
        void AddToQueue(StreamNotificationItem value);
        void StreamListCleanup();
    }
    public class StreamNotificationService : BaseQueueService<StreamNotificationItem>, IStreamNotificationService
    {
        public StreamNotificationService(IDbContextFactory dbContextFactory, IDatabaseMethodService databaseMethodService, ILoggerFactory loggerFactory) :base (dbContextFactory, databaseMethodService, loggerFactory){}
        

        public static List<LiveStreamer> LiveStreamerList { get; set; } = new();
        public static int StreamCheckDelay { get; } = 5;

        private protected override async Task ProcessQueueAsync()
        {
            foreach (StreamNotificationItem streamNotificationItem in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                try
                {
                    DiscordMember streamMember = await streamNotificationItem.Guild.GetMemberAsync(streamNotificationItem.EventArgs.User.Id);
                    if (streamNotificationItem.EventArgs.User == null || streamNotificationItem.EventArgs.User.Presence?.Activities == null) continue;
                    DiscordActivity activity = streamNotificationItem.EventArgs.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube");
                    if (activity?.RichPresence?.State == null || activity.RichPresence?.Details == null || activity.StreamUrl == null) continue;
                    string gameTitle = activity.RichPresence.State;
                    string streamTitle = activity.RichPresence.Details;
                    string streamUrl = activity.StreamUrl;

                    var roleIds = new HashSet<ulong>(streamNotificationItem.StreamNotification.RoleIds ?? Array.Empty<ulong>());
                    var games = new HashSet<string>(streamNotificationItem.StreamNotification.Games ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

                    bool role = roleIds.Count == 0 || streamMember.Roles.Any(r => roleIds.Contains(r.Id));
                    bool game = games.Count == 0 || games.Contains(gameTitle);

                    if (!game || !role) continue;
                    string description = $"**Streamer:**\n {streamNotificationItem.EventArgs.User.Mention}\n\n" +
                                         $"**Game:**\n{gameTitle}\n\n" +
                                         $"**Stream title:**\n{streamTitle}\n\n" +
                                         $"**Stream Link:**\n{streamUrl}";
                    DiscordEmbedBuilder embed = new()
                    {
                        Color = new DiscordColor(0x6441A5),
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            IconUrl = streamNotificationItem.EventArgs.User.AvatarUrl,
                            Name = "STREAM",
                            Url = streamUrl
                        },
                        Description = description,
                        Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                        {
                            Url = streamNotificationItem.EventArgs.User.AvatarUrl
                        },
                        Title = $"Check out {streamNotificationItem.EventArgs.User.Username} is now Streaming!"
                    };
                    await streamNotificationItem.Channel.SendMessageAsync(embed: embed);
                    _logger.LogInformation("Stream notification sent for {Username} in {GuildName} in {Channel}",
                        streamNotificationItem.EventArgs.User.Username,
                        streamNotificationItem.Guild.Name,
                        streamNotificationItem.Channel.Name);
                    //adds user to list
                    LiveStreamerList.Add(streamNotificationItem.Streamer);

                }
                catch (Exception e)
                {
                    _logger.LogError("{} failed to process item in queue \n{}", this.GetType().Name,e);
                    continue;
                }
            }
        }

        public void StreamListCleanup()
        {
            try
            {
                foreach (LiveStreamer item in LiveStreamerList.Where(item => item.Time.AddHours(StreamCheckDelay) < DateTime.UtcNow && item.User.Presence.Activity.ActivityType != ActivityType.Streaming))
                {
                    _logger.LogDebug(CustomLogEvents.LiveStream, "User {UserName} removed from Live Stream List - {CheckDelay} hours passed.", item.User.Username, StreamCheckDelay);
                    LiveStreamerList.Remove(item);
                }
            }
            catch (Exception)
            {
                _logger.LogDebug(CustomLogEvents.LiveStream, "Live Stream list is empty. No-one to remove or check.");
            }
        }
    }


    public class StreamNotificationItem
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
