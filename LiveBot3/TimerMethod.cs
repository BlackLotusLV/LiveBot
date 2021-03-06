﻿using DSharpPlus.Entities;
using LiveBot.Automation;
using LiveBot.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace LiveBot
{
    internal static class TimerMethod
    {
        private static DateTime TCHubLastUpdated;
        public static byte[][] EventLogoBitArr { get; set; } = new byte[9][];
        public static byte[,][] RewardsImageBitArr { get; set; } = new byte[4, 4][];

        public static void StreamListCheck(List<LiveStreamer> list, int StreamCheckDelay)
        {
            try
            {
                foreach (var item in list)
                {
                    if (item.Time.AddHours(StreamCheckDelay) < DateTime.Now && item.User.Presence.Activity.ActivityType != ActivityType.Streaming)
                    {
                        Program.Client.Logger.LogInformation(CustomLogEvents.LiveStream, $"User {item.User.Username} removed from Live Stream List - {LiveStream.StreamCheckDelay} hours passed.");
                        list.Remove(item);
                    }
                }
            }
            catch (Exception)
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.LiveStream, "Live Stream list is empty. No-one to remove or check.");
            }
        }

        public async static Task ActivatedRolesCheck(List<ActivateRolesTimer> list)
        {
            try
            {
                foreach (var item in list)
                {
                    if (item.Time.AddMinutes(5) < DateTime.Now)
                    {
                        await item.Role.ModifyAsync(mentionable: false);
                        list.Remove(item);
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("[System] ActivateRolesTimer list is empty!");
            }
        }

        public static void UpdateHubInfo(bool forced = false)
        {
            List<TCHubJson.Summit> JSummit;
            DateTime endtime;
            using WebClient wc = new();
            bool Connected = true;
            string JSummitString = string.Empty;
            try
            {
                JSummitString = wc.DownloadString(Program.TCHubJson.Summit);
            }
            catch (WebException e)
            {
                Connected = false;
                Program.Client.Logger.LogInformation(CustomLogEvents.TCHub, e, "Connection error. Either wrong API link, or the Hub is down.");
            }
            if (Connected)
            {
                JSummit = JsonConvert.DeserializeObject<List<TCHubJson.Summit>>(JSummitString);
                if (forced)
                {
                    endtime = TCHubLastUpdated;
                }
                else
                {
                    endtime = CustomMethod.EpochConverter(JSummit[0].End_Date * 1000);
                }
                if (endtime != TCHubLastUpdated || forced)
                {
                    TCHubLastUpdated = endtime;
                    Program.TCHubDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(wc.DownloadString(Program.TCHubJson.Dictionary));
                    Program.JSummit = JSummit;
                    Program.TCHub = JsonConvert.DeserializeObject<TCHubJson.TCHub>(wc.DownloadString(Program.TCHubJson.GameData));
                    for (int i = 0; i < JSummit[0].Events.Length; i++)
                    {
                        var ThisEvent = JSummit[0].Events[i];
                        EventLogoBitArr[i] = wc.DownloadData($"https://www.thecrew-hub.com/gen/assets/summits/{ThisEvent.Img_Path}");
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < JSummit[i].Rewards.Length; j++)
                        {
                            RewardsImageBitArr[i, j] = wc.DownloadData($"https://www.thecrew-hub.com/gen/assets/summits/{JSummit[i].Rewards[j].Img_Path}");
                        }
                    }
                    Program.Client.Logger.LogInformation(CustomLogEvents.TCHub, $"Info downloaded for {JSummit[0].Summit_ID} summit.");
                }
            }
        }

        public static void DownloadHubNews()
        {
            using WebClient wc = new();
            wc.Headers.Add("Ubi-AppId", "dda77324-f9d6-44ea-9ecb-30e57b286f6d");
            wc.Headers.Add("Ubi-localeCode", "us-en");
            string NewsString = string.Empty;
            bool Connected = true;

            try
            {
                NewsString = wc.DownloadString(Program.TCHubJson.News);
            }
            catch (WebException e)
            {
                Connected = false;
                Program.Client.Logger.LogInformation(CustomLogEvents.TCHub, e, "Connection error. Either wrong API link, or the Hub is down.");
            }
            if (Connected)
            {
                Program.TCHub = JsonConvert.DeserializeObject<TCHubJson.TCHub>(NewsString);
            }
        }
    }
}