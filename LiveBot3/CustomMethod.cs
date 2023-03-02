﻿using DSharpPlus.CommandsNext;
using LiveBot.DB;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace LiveBot
{
    internal static class CustomMethod
    {
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