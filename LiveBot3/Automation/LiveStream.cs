using LiveBot.Services;

namespace LiveBot.Automation
{
    internal class LiveStream
    {
        private readonly IStreamNotificationService _streamNotificationService;

        public LiveStream(IStreamNotificationService streamNotificationService)
        {
            _streamNotificationService = streamNotificationService;
        }

        private static List<LiveStreamer> LiveStreamerList { get; } = new();

        private static readonly int StreamCheckDelay = 5;

        public async Task Stream_Notification(object client, PresenceUpdateEventArgs e)
        {
            if (e.User == null || e.User.IsBot || e.User.Presence == null) return;
            DiscordGuild guild = e.User.Presence.Guild;
            List<DB.StreamNotifications> streamNotifications = DB.DBLists.StreamNotifications.Where(w => w.Server_ID == guild.Id).ToList();
            if (streamNotifications.Count < 1) return;
            foreach (var streamNotification in streamNotifications)
            {
                DiscordChannel channel = guild.GetChannel(streamNotification.Channel_ID);
                if (!Program.ServerIdList.Contains(guild.Id)) return;
                LiveStreamer streamer = new()
                {
                    User = e.User,
                    Time = DateTime.UtcNow,
                    Guild = guild,
                    Channel = channel
                };
                int itemIndex;
                try
                {
                    itemIndex = LiveStreamerList.FindIndex(a =>
                        a.User.Id == e.User.Id
                        && a.Guild.Id == e.User.Presence.Guild.Id
                        && a.Channel.Id == channel.Id);
                }
                catch (Exception)
                {
                    itemIndex = -1;
                }
                if (itemIndex >= 0
                    && e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube") == null)
                {
                    //removes user from list
                    if (LiveStreamerList[itemIndex].Time.AddHours(StreamCheckDelay) < DateTime.UtcNow
                        && e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube") == LiveStreamerList[itemIndex].User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube"))
                    {
                        LiveStreamerList.RemoveAt(itemIndex);
                    }
                }
                else if (itemIndex == -1
                && e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube") != null
                && e.User.Presence.Activities.First(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube").ActivityType.Equals(ActivityType.Streaming))
                {
                    _streamNotificationService.QueueStream(streamNotification, e, guild, channel, streamer);
                }
            }
            await Task.Delay(1);
        }
    }
}