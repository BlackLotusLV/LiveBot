using DSharpPlus.CommandsNext;
using LiveBot.DB;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace LiveBot
{
    internal static class CustomMethod
    {
        public static string ScoreToTime(int time)
        {
            StringBuilder[] sTime = { new StringBuilder(), new StringBuilder() };
            for (var i = 0; i < time.ToString().Length; i++)
            {
                if (i < time.ToString().Length - 3)
                {
                    sTime[0].Append(time.ToString()[i]);
                }
                else
                {
                    sTime[1].Append(time.ToString()[i]);
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
            return seconds.Hours == 0 ? $"{seconds.Minutes}:{seconds.Seconds}.{sTime[1]}" : $"{seconds.Hours}:{seconds.Minutes}:{seconds.Seconds}.{sTime[1]}";
        }

        /// <summary>
        /// Sends a message in the moderator log channel
        /// </summary>
        /// <param name="modLogChannel">Channel where the message will be sent.</param>
        /// <param name="targetUser">The user against who an action is being taken against.</param>
        /// <param name="description">The description of the action taken.</param>
        /// <param name="type">The type of action taken.</param>
        /// <param name="content">Additional content outside of the embed</param>
        /// <returns></returns>
        public static async Task SendModLogAsync(DiscordChannel modLogChannel, DiscordUser targetUser, string description, ModLogType type, string content = null)
        {
            DiscordColor color = DiscordColor.NotQuiteBlack;
            var footerText = string.Empty;
            switch (type)
            {
                case ModLogType.Kick:
                    color = new DiscordColor(0xf90707);
                    footerText = "User Kicked";
                    break;

                case ModLogType.Ban:
                    color = new DiscordColor(0xf90707);
                    footerText = "User Banned";
                    break;

                case ModLogType.Info:
                    color = new DiscordColor(0x59bfff);
                    footerText = "Info";
                    break;

                case ModLogType.Warning:
                    color = new DiscordColor(0xFFBA01);
                    footerText = "User Warned";
                    break;

                case ModLogType.Unwarn:
                    footerText = "User Unwarned";
                    break;

                case ModLogType.Unban:
                    footerText = "User Unbanned";
                    break;

                case ModLogType.TimedOut:
                    color = new DiscordColor(0xFFBA01);
                    footerText = "User Timed Out";
                    break;

                case ModLogType.TimeOutRemoved:
                    footerText = "User Timeout Removed";
                    break;

                default:
                    break;
            }
            DiscordMessageBuilder discordMessageBuilder = new();
            DiscordEmbedBuilder discordEmbedBuilder = new()
            {
                Color = color,
                Description = description,
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = targetUser.AvatarUrl,
                    Name = $"{targetUser.Username} ({targetUser.Id})"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    IconUrl = targetUser.AvatarUrl,
                    Text = footerText
                }
            };

            discordMessageBuilder.AddEmbed(discordEmbedBuilder);
            discordMessageBuilder.Content = content;

            await modLogChannel.SendMessageAsync(discordMessageBuilder);
        }

        /// <summary>
        /// Checks if the user has the required permissions to use the command
        /// </summary>
        /// <param name="member"></param>
        /// <returns>If user has permissions, returns true</returns>
        public static bool CheckIfMemberAdmin(DiscordMember member)
        {
            return member.Permissions.HasPermission(Permissions.ManageMessages) ||
                   member.Permissions.HasPermission(Permissions.KickMembers) ||
                   member.Permissions.HasPermission(Permissions.BanMembers) ||
                   member.Permissions.HasPermission(Permissions.Administrator);
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