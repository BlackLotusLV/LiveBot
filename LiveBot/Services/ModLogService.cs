﻿using System.Net.Http;
using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Services;

public interface IModLogService
{
    public void StartService(DiscordClient client);
    public void StopService();
    public void AddToQueue(ModLogItem value);
}

public class ModLogService : BaseQueueService<ModLogItem>,IModLogService
{
    public ModLogService(IDbContextFactory<LiveBotDbContext> dbContextFactory, IDatabaseMethodService databaseMethodService, ILoggerFactory loggerFactory) : base(dbContextFactory, databaseMethodService,loggerFactory){}

    private protected override async Task ProcessQueueAsync()
    {
        foreach (ModLogItem modLogItem in Queue.GetConsumingEnumerable(CancellationTokenSource.Token))
        {
            try
            {
                await SendModLogAsync(modLogItem);
            }
            catch (Exception e)
            {
                Logger.LogError(CustomLogEvents.ServiceError,e,"{Type} failed to process item in queue",GetType().Name);
            }
        }
    }

    private async Task SendModLogAsync(ModLogItem item)
    {
        DiscordColor color = DiscordColor.NotQuiteBlack;
            var footerText = string.Empty;
            switch (item.Type)
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

                case ModLogType.UnWarn:
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
                case ModLogType.TimeOutExtended:
                    color = new DiscordColor(0xFFBA01);
                    footerText = "User Timeout Extended";
                    break;
                case ModLogType.TimeOutShortened:
                    color = new DiscordColor(0xFFBA01);
                    footerText = "User Timeout Shortened";
                    break;
                default:
                    Logger.LogDebug(CustomLogEvents.ModLog, "ModLogType not found: {Type}", item.Type);
                    break;
            }
            DiscordMessageBuilder discordMessageBuilder = new();
            DiscordEmbedBuilder discordEmbedBuilder = new()
            {
                Color = color,
                Description = item.Description,
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = item.TargetUser.AvatarUrl,
                    Name = $"{item.TargetUser.Username} ({item.TargetUser.Id})"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    IconUrl = item.TargetUser.AvatarUrl,
                    Text = footerText
                }
            };
            discordMessageBuilder.Content = item.Content;
            var hasAttachment = false;
            MemoryStream memoryStream = new();
            if (item.Attachment!= null)
            {
                using HttpClient client = new();
                HttpResponseMessage response = await client.GetAsync(item.Attachment.Url);
                if (response.IsSuccessStatusCode)
                {
                    await response.Content.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    discordMessageBuilder.AddFile(item.Attachment.FileName, memoryStream);
                    discordEmbedBuilder.ImageUrl = $"attachment://{item.Attachment.FileName}";
                    hasAttachment = true;
                }
            }
            discordMessageBuilder.AddEmbed(discordEmbedBuilder);

            DiscordMessage sentMsg = await item.ModLogChannel.SendMessageAsync(discordMessageBuilder);
            if (hasAttachment)
            {
                DiscordMessage renewed = await item.ModLogChannel.GetMessageAsync(sentMsg.Id);
                await DatabaseMethodService.AddInfractionsAsync(
                    new Infraction(
                        GetBotUser().Id,
                        item.TargetUser.Id,
                        item.ModLogChannel.Guild.Id,
                        renewed.Embeds[0].Image.Url.ToString(),
                        true,
                        InfractionType.Note)
                );
            }
            await memoryStream.DisposeAsync();
    }
}
public enum ModLogType
{
    Kick,
    Ban,
    Info,
    Warning,
    UnWarn,
    Unban,
    TimedOut,
    TimeOutRemoved,
    TimeOutExtended,
    TimeOutShortened
}

public class ModLogItem
{
    public DiscordChannel ModLogChannel { get; set; }
    public DiscordUser TargetUser { get; set; }
    public string Description { get; set; }
    public ModLogType Type { get; set; }
    public string Content { get; set; }
    public DiscordAttachment Attachment { get; set; }
    public ModLogItem(DiscordChannel modLogChannel, DiscordUser targetUser, string description, ModLogType type, string content=null, DiscordAttachment attachment=null)
    {
        ModLogChannel = modLogChannel;
        TargetUser = targetUser;
        Description = description;
        Type = type;
        Content = content;
        Attachment = attachment;
    }
}