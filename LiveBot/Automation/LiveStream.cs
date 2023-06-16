using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation
{
    internal class LiveStream
    {
        private readonly IStreamNotificationService _streamNotificationService;
        private readonly LiveBotDbContext _dbContext;

        public LiveStream(IStreamNotificationService streamNotificationService, LiveBotDbContext dbContext)
        {
            _streamNotificationService = streamNotificationService;
            _dbContext = dbContext;
        }

        public async Task Stream_Notification(object client, PresenceUpdateEventArgs e)
        {
            if (e.User is null || e.User.IsBot || e.User.Presence is null) return;
            DiscordGuild guild = e.User.Presence.Guild;
            if (e.User.Presence.Activities.All(x => x.ActivityType != ActivityType.Streaming)) return;
            var streamNotifications = _dbContext.StreamNotifications.Where(w => w.GuildId == guild.Id).ToList();
            if (streamNotifications.Count < 1) return;
            foreach (StreamNotifications streamNotification in streamNotifications)
            {
                DiscordChannel channel = guild.GetChannel(streamNotification.ChannelId);
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
                    itemIndex = StreamNotificationService.LiveStreamerList.FindIndex(a =>
                        a.User.Id == e.User.Id
                        && a.Guild.Id == e.User.Presence.Guild.Id
                        && a.Channel.Id == channel.Id);
                }
                catch (Exception)
                {
                    itemIndex = -1;
                }

                switch (itemIndex)
                {
                    case >= 0
                        when e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube") == null:
                    {
                        //removes user from list
                        if (StreamNotificationService.LiveStreamerList[itemIndex].Time.AddHours(StreamNotificationService.StreamCheckDelay) < DateTime.UtcNow
                            && e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube") == StreamNotificationService.LiveStreamerList[itemIndex]
                                .User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube"))
                        {
                            StreamNotificationService.LiveStreamerList.RemoveAt(itemIndex);
                        }

                        break;
                    }
                    case -1
                        when e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube") != null
                             && e.User.Presence.Activities.First(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube").ActivityType.Equals(ActivityType.Streaming):
                        _streamNotificationService.AddToQueue(new StreamNotificationItem(streamNotification, e, guild, channel, streamer));
                        break;
                }
            }
        }
    }
}