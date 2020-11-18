﻿using DSharpPlus.Entities;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveBot.Automation
{
    internal static class Weather
    {
        private static DiscordChannel WeatherChannel;
        private static int Interval = Timeout.Infinite;
        private static string OldWeather = string.Empty;
        private static readonly Timer WeatherTimer = new Timer(async e =>await CheckWeather(), null, Timeout.Infinite, Interval);

        public static void StartTimer()
        {
            if (Interval == Timeout.Infinite && !Program.TestBuild)
            {
                Interval = 15000;
                WeatherTimer.Change(0, Interval);
                WeatherChannel = Program.TCGuild.GetChannel(700414491749253220);
                Console.WriteLine("weather timer started");
            }
        }

        private static async Task CheckWeather()
        {
            StringBuilder sb = new StringBuilder();
            TimeSpan now = DateTime.UtcNow.TimeOfDay;
            TimeSpan CurrentTime = new TimeSpan(now.Hours, now.Minutes, 0);
            TimeSpan TimeNow = new TimeSpan(now.Hours, now.Minutes, 0);
            string weathercondition = string.Empty;

            var Weather = DB.DBLists.WeatherSchedule.Where(w => w.Time >= CurrentTime && w.Day.Equals((int)DateTime.Today.DayOfWeek)).OrderBy(o => o.Time).ToList();
            if (Weather.Count < 60)
            {
                int day = (int)DateTime.Today.DayOfWeek + 1;
                if (day is 7)
                {
                    day = 0;
                }
                try
                {
                    Weather.AddRange(DB.DBLists.WeatherSchedule.Where(w => w.Day.Equals(day)).OrderBy(o => o.Time).ToList().GetRange(0, 61 - Weather.Count));
                }
                catch (Exception)
                {
                    Weather.AddRange(DB.DBLists.WeatherSchedule.Where(w => w.Day.Equals(day)).OrderBy(o => o.Time).ToList());
                }
            }
            sb.AppendLine($"**--------------------------------------------------------**");
            CurrentTime = CurrentTime.Add(TimeSpan.FromMinutes(59));
            for (int i = 0; i < 60; i++)
            {
                var WeatherSpeciffic = Weather.FirstOrDefault(w => w.Time.Hours.Equals(CurrentTime.Hours) && w.Time.Minutes.Equals(CurrentTime.Minutes));
                if (WeatherSpeciffic is null)
                {
                    sb.AppendLine($"{CurrentTime:hh\\:mm} - Weather is unknown");
                }
                else
                {
                    switch (WeatherSpeciffic.Weather)
                    {
                        case "clear":
                            weathercondition = ":sunny: **Clear**";
                            break;

                        case "*":
                            weathercondition = ":fog: **Fog**";
                            break;

                        case "rain":
                            weathercondition = ":cloud_rain: **Rain**";
                            break;

                        case "rain*":
                            weathercondition = ":fog::cloud_rain: **Fog and Rain**";
                            break;

                        case "snow":
                            weathercondition = ":snowflake: **Snow**";
                            break;

                        case "snow*":
                            weathercondition = ":fog::snowflake: **Fog and Snow**";
                            break;
                    }
                    sb.AppendLine($"{WeatherSpeciffic.Time:hh\\:mm} - {weathercondition}");
                }
                CurrentTime -= TimeSpan.FromMinutes(1);
            }
            sb.AppendLine($"Current UTC time is {TimeNow:hh\\:mm}. Here is the weather for the upcoming hour!");
            sb.AppendLine($"**--------------------------------------------------------**");
            string weathertext = sb.ToString();
            if (OldWeather != weathertext)
            {
                var messages = await WeatherChannel.GetMessagesAsync(10);
                if (messages.Count == 0 || !messages[0].Author.Equals(Program.Client.CurrentUser))
                {
                    if (messages.Count != 0)
                    {
                        await WeatherChannel.DeleteMessagesAsync(messages);
                    }
                    await WeatherChannel.SendMessageAsync(weathertext);
                }
                else if (messages[0].Author.Equals(Program.Client.CurrentUser))
                {
                    await messages[0].ModifyAsync(weathertext);
                }
                OldWeather = weathertext;
            }
        }
    }
}