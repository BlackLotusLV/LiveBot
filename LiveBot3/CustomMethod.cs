﻿using DSharpPlus.CommandsNext;
using Newtonsoft.Json;

namespace LiveBot
{
    internal static class CustomMethod
    {//testing pull request thing
        /// <summary>
        /// Gets the database connection string
        /// </summary>
        /// <returns>Returns connection string</returns>
        public static string GetConnString()
        {
            string json;
            using (var sr = new StreamReader(File.OpenRead("Config.json"), new UTF8Encoding(false)))
                json = sr.ReadToEnd();
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson.Config>(json).DataBase;
            return $"Host={cfgjson.Host};Username={cfgjson.Username};Password={cfgjson.Password};Database={cfgjson.Database}; Port={cfgjson.Port}";
        }

        /// <summary>
        /// Converts epoch time to datetime
        /// </summary>
        /// <param name="ms"></param>
        /// <returns>A datetime based on epoch</returns>
        public static DateTime EpochConverter(long ms)
        {
            DateTime f = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return f.AddMilliseconds(ms);
        }

        public static string ScoreToTime(int Time)
        {
            StringBuilder[] sTime = { new StringBuilder(), new StringBuilder() };
            for (int i = 0; i < Time.ToString().Length; i++)
            {
                if (i < Time.ToString().Length - 3)
                {
                    sTime[0].Append(Time.ToString()[i]);
                }
                else
                {
                    sTime[1].Append(Time.ToString()[i]);
                }
            }
            if (sTime[0].Length == 0)
            {
                sTime[0].Append('0');
            }
            while (sTime[1].Length < 3)
            {
                sTime[1].Insert(0, '0');
            }
            TimeSpan seconds = TimeSpan.FromSeconds(double.Parse(sTime[0].ToString()));
            if (seconds.Hours == 0)
            {
                return $"{seconds.Minutes}:{seconds.Seconds}.{sTime[1]}";
            }

            return $"{seconds.Hours}:{seconds.Minutes}:{seconds.Seconds}.{sTime[1]}";
        }

        public static string GetMissionList(List<Json.TCHubJson.Mission> MissionList, int page)
        {
            StringBuilder Missions = new();
            Missions.AppendLine("```csharp");
            for (int i = (page * 10) - 10; i < page * 10; i++)
            {
                Missions.AppendLine($"{i}\t{MissionList[i].ID}\t{HubMethods.NameIDLookup(MissionList[i].Text_ID)}");
            }
            Missions.Append("```");
            return Missions.ToString();
        }

        /// <summary>
        /// Sends a message in the moderator log channel
        /// </summary>
        /// <param name="ModLogChannel">Channel where the message will be sent.</param>
        /// <param name="TargetUser">The user against who an action is being taken against.</param>
        /// <param name="Description">The description of the action taken.</param>
        /// <param name="type">The type of action taken.</param>
        /// <param name="Content">Additional content outside of the embed</param>
        /// <returns></returns>
        public static async Task SendModLogAsync(DiscordChannel ModLogChannel, DiscordUser TargetUser, string Description, ModLogType type, string Content = null)
        {
            DiscordColor color = DiscordColor.NotQuiteBlack;
            string FooterText = string.Empty;
            switch (type)
            {
                case ModLogType.Kick:
                    color = new DiscordColor(0xf90707);
                    FooterText = "User Kicked";
                    break;

                case ModLogType.Ban:
                    color = new DiscordColor(0xf90707);
                    FooterText = "User Banned";
                    break;

                case ModLogType.Info:
                    color = new DiscordColor(0x59bfff);
                    FooterText = "Info";
                    break;

                case ModLogType.Warning:
                    color = new DiscordColor(0xFFBA01);
                    FooterText = "User Warned";
                    break;

                case ModLogType.Unwarn:
                    FooterText = "User Unwarned";
                    break;

                case ModLogType.Unban:
                    FooterText = "User Unbanned";
                    break;

                case ModLogType.TimedOut:
                    color = new DiscordColor(0xFFBA01);
                    FooterText = "User Timed Out";
                    break;

                case ModLogType.TimeOutRemoved:
                    FooterText = "User Timeout Removed";
                    break;

                default:
                    break;
            }
            DiscordMessageBuilder discordMessageBuilder = new();
            DiscordEmbedBuilder discordEmbedBuilder = new()
            {
                Color = color,
                Description = Description,
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = TargetUser.AvatarUrl,
                    Name = $"{TargetUser.Username} ({TargetUser.Id})"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    IconUrl = TargetUser.AvatarUrl,
                    Text = FooterText
                }
            };

            discordMessageBuilder.AddEmbed(discordEmbedBuilder);
            discordMessageBuilder.Content = Content;

            await ModLogChannel.SendMessageAsync(discordMessageBuilder);
        }

        /// <summary>
        /// Checks if the user has the required permissions to use the command
        /// </summary>
        /// <param name="member"></param>
        /// <returns>If user has permissions, returns true</returns>
        public static bool CheckIfMemberAdmin(DiscordMember member)
        {
            if (
                member.Permissions.HasPermission(Permissions.ManageMessages) ||
                member.Permissions.HasPermission(Permissions.KickMembers) ||
                member.Permissions.HasPermission(Permissions.BanMembers) ||
                member.Permissions.HasPermission(Permissions.Administrator))
            {
                return true;
            }
            return false;
        }

        public static string GetCommandOutput(CommandContext ctx, string command, string language, DiscordMember member)
        {
            DB.DBLists.LoadBotOutputList();

            language ??= (ctx.Channel.Id) switch
                {
                    (150283740172517376) => "gb",
                    (249586001167515650) => "de",
                    (253231012492869632) => "fr",
                    (410790788738580480) => "nl",
                    (410835311602565121) => "se",
                    (363977914196295681) => "ru",
                    (423845614686699521) => "lv",
                    (585529567708446731) => "es",
                    (741656080051863662) => "jp",
                    _ => "gb"
                };
            member ??= ctx.Member;

            var OutputEntry = DB.DBLists.BotOutputList.FirstOrDefault(w => w.Command.Equals(command) && w.Language.Equals(language));
            if (OutputEntry is null)
            {
                OutputEntry = DB.DBLists.BotOutputList.FirstOrDefault(w => w.Command.Equals(command) && w.Language.Equals("gb"));
                if (OutputEntry is null)
                {
                    return $"{ctx.Member.Mention}, Command output not found. Contact an admin.";
                }
            }
            return $"{member.Mention}, {OutputEntry.Command_Text}";
        }

        public static DiscordEmbed GetUserWarnings(DiscordGuild Guild, DiscordUser User, bool AdminCommand = false)
        {
            DB.DBLists.LoadServerRanks();
            DB.DBLists.LoadWarnings();
            int kcount = 0,
                bcount = 0,
                wlevel = 0,
                wcount = 0,
                splitcount = 1;
            StringBuilder Reason = new();
            var UserStats = DB.DBLists.ServerRanks.FirstOrDefault(f => User.Id == f.User_ID && Guild.Id == f.Server_ID);
            if (UserStats == null)
            {
                Services.LeaderboardService.AddToServerLeaderboard(User, Guild);
                UserStats = DB.DBLists.ServerRanks.FirstOrDefault(f => User.Id == f.User_ID && Guild.Id == f.Server_ID);
            }
            kcount = UserStats.Kick_Count;
            bcount = UserStats.Ban_Count;
            wlevel = UserStats.Warning_Level;
            var WarningsList = DB.DBLists.Warnings.Where(w => w.User_ID == User.Id && w.Server_ID == Guild.Id).OrderBy(w => w.Time_Created).ToList();
            if (!AdminCommand)
            {
                WarningsList.RemoveAll(w => w.Type == "note");
            }
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
                        if (item.Active)
                        {
                            Reason.Append("[✅] ");
                        }
                        else
                        {
                            Reason.Append("[❌] ");
                        }
                        break;
                }
                string addedInfraction = $"**ID:**{item.ID_Warning}\t**By:** <@{item.Admin_ID}>\t**Date:** <t:{(int)(item.Time_Created - new DateTime(1970, 1, 1)).TotalSeconds}>\n**Reason:** {item.Reason}\n **Type:**\t{item.Type}";

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

        public static void DBProgress(int LoadedTableCount, TimeSpan time, string DataTableName = null)
        {
            StringBuilder sb = new();
            sb.Append('[');
            for (int i = 1; i <= DB.DBLists.TableCount; i++)
            {
                if (i <= LoadedTableCount)
                {
                    sb.Append('#');
                }
                else
                {
                    sb.Append(' ');
                }
            }
            sb.Append(((float)LoadedTableCount / (float)DB.DBLists.TableCount).ToString(@$"] - [0.00%] [{time.Seconds}\.{time.Milliseconds:D3}]"));
            Program.Client.Logger.LogInformation(CustomLogEvents.POSTGRESQL, "{DataBase}", DataTableName is null ? "Starting to load Data Base" : $"{DataTableName} List Loaded");
            Program.Client.Logger.LogInformation(CustomLogEvents.POSTGRESQL, "{LoadBar}", sb.ToString());
            if (LoadedTableCount == DB.DBLists.TableCount)
            {
                DB.DBLists.LoadedTableCount = 0;
            }
        }

        public enum ModLogType
        {
            Kick,
            Ban,
            Info,
            Warning,
            Unwarn,
            Unban,
            TimedOut,
            TimeOutRemoved
        }
    }
}