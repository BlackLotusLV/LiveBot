﻿using System.Collections.Concurrent;
using System.Threading;

namespace LiveBot.Services
{
    internal static class StreamNotificationService
    {
        private static readonly ConcurrentQueue<StreamNotifItem> _notifications = new();

        // Use a CancellationTokenSource and CancellationToken to be able to stop the thread
        private static readonly CancellationTokenSource cts = new();
        private static readonly CancellationToken token = cts.Token;

        private static readonly Thread Notificationthread = new(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_notifications.IsEmpty)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    if (_notifications.TryDequeue(out StreamNotifItem item))
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
        public static void StartService()
        {
            Notificationthread.Start();
            Program.Client.Logger.LogInformation(CustomLogEvents.LiveBot, "Stream Notification Service started!");
        }
        public static void StopService()
        {
            // Request cancellation of the thread
            cts.Cancel();

            // Wait for the thread to finish
            Notificationthread.Join();

            Program.Client.Logger.LogInformation(CustomLogEvents.LiveBot, "Leaderboard service stopped!");
        }

        public static void QueueStream(DB.StreamNotifications StreamNotification, PresenceUpdateEventArgs e, DiscordGuild guild, DiscordChannel channel, Automation.LiveStreamer streamer)
        {
            _notifications.Enqueue(new StreamNotifItem(StreamNotification,e,guild,channel,streamer));
        }
        public static async Task StreamNotificationAsync(DB.StreamNotifications StreamNotification, PresenceUpdateEventArgs e, DiscordGuild guild, DiscordChannel channel, Automation.LiveStreamer streamer)
        {
            DiscordMember StreamMember = await guild.GetMemberAsync(e.User.Id);
            bool role = false, game = false;
            if (e.User?.Presence?.Activities == null) return;
            DiscordActivity activity = e.User.Presence.Activities.FirstOrDefault(w => w.Name.ToLower() == "twitch" || w.Name.ToLower() == "youtube");
            if (activity.RichPresence?.State == null || activity.RichPresence?.Details == null || activity.StreamUrl == null) return;
            string gameTitle = activity.RichPresence.State;
            string streamTitle = activity.RichPresence.Details;
            string streamURL = activity.StreamUrl;
            if (StreamNotification.Roles_ID != null)
            {
                foreach (DiscordRole urole in StreamMember.Roles)
                {
                    foreach (decimal roleid in StreamNotification.Roles_ID)
                    {
                        if (urole.Id == roleid)
                        {
                            role = true;
                            break;
                        }
                    }
                }
            }
            else if (StreamNotification.Roles_ID == null)
            {
                role = true;
            }
            if (StreamNotification.Games != null)
            {
                foreach (string ugame in StreamNotification.Games)
                {
                    try
                    {
                        if (gameTitle == ugame)
                        {
                            game = true;
                            break;
                        }
                    }
                    catch { game = false; }
                }
            }
            else if (StreamNotification.Games == null)
            {
                game = true;
            }
            if (game && role)
            {
                DiscordEmbedBuilder embed = new()
                {
                    Color = new DiscordColor(0x6441A5),
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        IconUrl = e.User.AvatarUrl,
                        Name = "STREAM",
                        Url = streamURL
                    },
                    Description = $"**Streamer:**\n {e.User.Mention}\n\n" +
            $"**Game:**\n{gameTitle}\n\n" +
            $"**Stream title:**\n{streamTitle}\n\n" +
            $"**Stream Link:**\n{streamURL}",
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                    {
                        Url = e.User.AvatarUrl
                    },
                    Title = $"Check out {e.User.Username} is now Streaming!"
                };
                await channel.SendMessageAsync(embed: embed);
                //adds user to list
                Automation.LiveStream.LiveStreamerList.Add(streamer);
            }
        }
    }


    internal class StreamNotifItem
    {
        public DB.StreamNotifications StreamNotification { get; set; }
        public PresenceUpdateEventArgs EventArgs { get; set; }
        public DiscordGuild Guild { get; set; }
        public DiscordChannel Channel { get; set; }
        public Automation.LiveStreamer Streamer { get; set; }
        public StreamNotifItem(DB.StreamNotifications StreamNotification, PresenceUpdateEventArgs EventArgs, DiscordGuild Guild, DiscordChannel Channel, Automation.LiveStreamer Streamer)
        {
            this.StreamNotification = StreamNotification;
            this.EventArgs = EventArgs;
            this.Guild = Guild;
            this.Channel = Channel;
            this.Streamer = Streamer;
        }
    }
}
