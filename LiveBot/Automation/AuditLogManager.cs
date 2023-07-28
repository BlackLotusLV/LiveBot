#nullable enable
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace LiveBot.Automation;

public sealed class AuditLogManager
{
    private readonly IModLogService _modLogService;
    private readonly IDbContextFactory _dbContextFactory;
    private readonly IDatabaseMethodService _databaseMethodService;

    public AuditLogManager(IDbContextFactory dbContextFactory, IModLogService modLogService, IDatabaseMethodService databaseMethodService)
    {
        _modLogService = modLogService;
        _dbContextFactory = dbContextFactory;
        _databaseMethodService = databaseMethodService;
    }

    public async Task UnknownEventToAuditLog(DiscordClient client, UnknownEventArgs args)
    {
        if (args.EventName != "GUILD_AUDIT_LOG_ENTRY_CREATE") return;
        var auditLogEntry = JsonConvert.DeserializeObject<AuditLogEntry>(args.Json);
        if (auditLogEntry is null) return;
        switch (auditLogEntry.ActionType)
        {
            case AuditLogEvents.MemberBanAdd:
                await BanManager(client, auditLogEntry);
                break;
            case AuditLogEvents.MemberUpdate:
                await TimeOutLogger(client, auditLogEntry);
                break;
            case AuditLogEvents.MemberKick:
                await KickManager(client, auditLogEntry);
                break;
            case AuditLogEvents.MemberBanRemove:
                await UnBanManager(client, auditLogEntry);
                break;
            default:
                client.Logger.LogDebug(CustomLogEvents.AuditLogManager,"No integration created for AuditLog parser for {ActionType}", auditLogEntry.ActionType);
                break;
        }
        
    }

    private async Task KickManager(DiscordClient client, AuditLogEntry logEntry)
    {
        if (logEntry.ActionType != AuditLogEvents.MemberKick) return;
        await using LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Key == logEntry.GuildId).Value;
        Guild guildSettings = await liveBotDbContext.Guilds.FindAsync(guild.Id) ??
                              await _databaseMethodService.AddGuildAsync(new Guild(guild.Id));
        if (guildSettings.ModerationLogChannelId is null) return;
        DiscordChannel modLogChannel = guild.GetChannel(guildSettings.ModerationLogChannelId.Value);
        DiscordUser targetUser = await client.GetUserAsync(CheckIfIdNotNull(logEntry.TargetId));
        DiscordUser modUser = await client.GetUserAsync(CheckIfIdNotNull(logEntry.UserId));
        GuildUser guildUser = await liveBotDbContext.GuildUsers.FindAsync(targetUser.Id, guild.Id) ??
                              await _databaseMethodService.AddGuildUsersAsync(new GuildUser(targetUser.Id, guild.Id));

        guildUser.KickCount++;
        liveBotDbContext.GuildUsers.Update(guildUser);
        await liveBotDbContext.SaveChangesAsync();
        
        _modLogService.AddToQueue(new ModLogItem(
            modLogChannel,
            targetUser,
            "# User Kicked\n" +
            $"- **User:** {targetUser.Mention}\n" +
            $"- **Moderator:** {modUser.Mention}\n" +
            $"- **Reason:** {logEntry.Reason}\n" +
            $"- **Kick Count:** {guildUser.KickCount}",
            ModLogType.Kick));
        await _databaseMethodService.AddInfractionsAsync(
            new Infraction(
                modUser.Id,
                targetUser.Id,
                guild.Id,
                logEntry.Reason,
                false,
                InfractionType.Kick)
            );
        client.Logger.LogInformation(CustomLogEvents.AuditLogManager,"Kick logged for {User} in {Guild} by {ModUser}",targetUser.Username,guild.Name,modUser.Username);
    }

    private async Task UnBanManager(DiscordClient client, AuditLogEntry logEntry)
    {
        if (logEntry.ActionType != AuditLogEvents.MemberBanRemove) return;
        await using LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Key == logEntry.GuildId).Value;
        Guild guildSettings = await liveBotDbContext.Guilds.FindAsync(guild.Id) ??
                              await _databaseMethodService.AddGuildAsync(new Guild(guild.Id));
        if (guildSettings.ModerationLogChannelId is null) return;
        DiscordChannel modLogChannel = guild.GetChannel(guildSettings.ModerationLogChannelId.Value);
        DiscordUser targetUser = await client.GetUserAsync(CheckIfIdNotNull(logEntry.TargetId));
        DiscordUser modUser = await client.GetUserAsync(CheckIfIdNotNull(logEntry.UserId));
        _modLogService.AddToQueue(new ModLogItem(
            modLogChannel,
            targetUser,
            "# User Unbanned\n" +
            $"- **User:** {targetUser.Mention}\n" +
            $"- **Moderator:** {modUser.Mention}\n",
            ModLogType.Unban
            ));
        client.Logger.LogInformation(CustomLogEvents.AuditLogManager,"Unban logged for {User} in {Guild} by {ModUser}",targetUser.Username,guild.Name,modUser.Username);
    }

    private async Task TimeOutLogger(DiscordClient client, AuditLogEntry logEntry)
    {
        if (logEntry.ActionType != AuditLogEvents.MemberUpdate || logEntry.Changes is null) return;
        AuditLogChanges? changes = logEntry.Changes.FirstOrDefault(w => w.Key == "communication_disabled_until");
        if (changes is null) return;
        DateTimeOffset? newTime = null;
        DateTimeOffset? oldTime = null;
        if (DateTimeOffset.TryParse($"{changes.NewValue}", out DateTimeOffset newTimeParse))
        {
            newTime = newTimeParse;
        }
        if (DateTimeOffset.TryParse($"{changes.OldValue}", out DateTimeOffset oldTimeParse))
        {
            oldTime = oldTimeParse;
        }
        if (newTime is null && oldTime is null) return;
        await using LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Key == logEntry.GuildId).Value;
        Guild guildSettings= liveBotDbContext.Guilds.First(w => w.Id == logEntry.GuildId);
        if (guildSettings.ModerationLogChannelId is null) return;
        DiscordChannel modLogChannel = guild.GetChannel(guildSettings.ModerationLogChannelId.Value);
        DiscordUser targetUser = await client.GetUserAsync(CheckIfIdNotNull(logEntry.TargetId));
        DiscordUser modUser = await client.GetUserAsync(CheckIfIdNotNull(logEntry.UserId));
        ModLogType modLogType;
        string description;
        StringBuilder reasonBuilder = new();
        reasonBuilder.AppendLine(logEntry.Reason ?? "-reason not specified-");
        var infractionType = InfractionType.TimeoutRemoved;
        if (newTime is null && oldTime is not null)
        {
            modLogType = ModLogType.TimeOutRemoved;
            description ="# Timeout Removed\n" +
                         $"- **User:**{targetUser.Mention}\n" +
                         $"- **by:** {modUser.Mention}\n" +
                         $"- **old timeout:**<t:{oldTime.Value.ToUnixTimeSeconds()}:F>(<t:{oldTime.Value.ToUnixTimeSeconds()}:R>)";
            reasonBuilder.Append($"- **Old timeout:** <t:{oldTime.Value.ToUnixTimeSeconds()}:F>");
        }
        else if (oldTime < newTime && oldTime > DateTimeOffset.UtcNow)
        {
            modLogType = ModLogType.TimeOutExtended;
            description = $"# User Timeout Extended\n" +
                          $"- **User:**{targetUser.Mention}\n" +
                          $"- **by:** {modUser.Mention}\n" +
                          $"- **reason:** {logEntry.Reason??"-reason not specified-"}\n" +
                          $"- **until:**<t:{newTime.Value.ToUnixTimeSeconds()}:F>(<t:{newTime.Value.ToUnixTimeSeconds()}:R>)\n" +
                          $"- ***old timeout:**<t:{oldTime.Value.ToUnixTimeSeconds()}:F>(<t:{oldTime.Value.ToUnixTimeSeconds()}:R>)*";
            infractionType = InfractionType.TimeoutExtended;
            reasonBuilder.AppendLine($"- **Until:** <t:{newTime.Value.ToUnixTimeSeconds()}:F>)")
                .Append($"- **Old timeout:** <t:{oldTime.Value.ToUnixTimeSeconds()}:F>)");
        }
        else if ((oldTime is null && newTime>DateTimeOffset.UtcNow) || (oldTime < newTime && oldTime<DateTimeOffset.UtcNow))
        {
            modLogType = ModLogType.TimedOut;
            description ="# User Timed Out\n" +
                         $"- **User:**{targetUser.Mention}\n" +
                         $"- **by:** {modUser.Mention}\n" +
                         $"- **reason:** {logEntry.Reason??"-reason not specified-"}\n" +
                         $"- **until:**<t:{newTime.Value.ToUnixTimeSeconds()}:F>(<t:{newTime.Value.ToUnixTimeSeconds()}:R>)";
            infractionType =  InfractionType.TimeoutAdded;
            reasonBuilder.Append($"- **Until:** <t:{newTime.Value.ToUnixTimeSeconds()}:F>)");
        }
        else if (oldTime > newTime)
        {
            modLogType = ModLogType.TimeOutShortened;
            description = $"# User Timeout Shortened\n" +
                          $"- **User:**{targetUser.Mention}\n" +
                          $"- **by** {modUser.Mention}\n" +
                          $"- **reason:** {logEntry.Reason??"-reason not specified-"}\n" +
                          $"- **until:**<t:{newTime.Value.ToUnixTimeSeconds()}:F>(<t:{newTime.Value.ToUnixTimeSeconds()}:R>)\n" +
                          $"- ***old timeout:**<t:{oldTime.Value.ToUnixTimeSeconds()}:F>(<t:{oldTime.Value.ToUnixTimeSeconds()}:R>)*";
            infractionType = InfractionType.TimeoutReduced;
            reasonBuilder.AppendLine($"- **Until:** <t:{newTime.Value.ToUnixTimeSeconds()}:F>)")
                .Append($"- **Old timeout:** <t:{oldTime.Value.ToUnixTimeSeconds()}:F>)");
            
        }
        else return;
        _modLogService.AddToQueue(new ModLogItem(modLogChannel,targetUser,description,modLogType));
        await _databaseMethodService.AddInfractionsAsync(
            new Infraction(modUser.Id, targetUser.Id, guild.Id, reasonBuilder.ToString(),
                false, infractionType));
        client.Logger.LogInformation(CustomLogEvents.AuditLogManager,"{ModLogType} logged for {User} in {Guild} by {ModUser}",modLogType,targetUser.Username,guild.Name,modUser.Username);
    }
    
    private async Task BanManager(DiscordClient client, AuditLogEntry logEntry)
    {
        if (logEntry.ActionType != AuditLogEvents.MemberBanAdd) return;
        await using LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
        GuildUser guildUser = await liveBotDbContext.GuildUsers.FindAsync(CheckIfIdNotNull(logEntry.TargetId), logEntry.GuildId) ??
                              await _databaseMethodService.AddGuildUsersAsync(new GuildUser(CheckIfIdNotNull(logEntry.TargetId), logEntry.GuildId));
        guildUser.BanCount++;
        liveBotDbContext.Update(guildUser);
        await liveBotDbContext.SaveChangesAsync();
        
        Guild? guildSettings = await liveBotDbContext.Guilds.FirstOrDefaultAsync(w => w.Id == logEntry.GuildId);
        if (guildSettings?.ModerationLogChannelId is null) return;
        DiscordGuild guild = client.Guilds.FirstOrDefault(w => w.Key == guildSettings.Id).Value;
        DiscordChannel modLogChannel = guild.GetChannel(guildSettings.ModerationLogChannelId.Value);
        DiscordUser targetUser = await client.GetUserAsync(CheckIfIdNotNull(logEntry.TargetId));
        DiscordUser moderator = await client.GetUserAsync(CheckIfIdNotNull(logEntry.UserId));
        _modLogService.AddToQueue(new ModLogItem(
            modLogChannel,
            targetUser,
            "# User Banned\n" +
            $"- **User:** {targetUser.Mention}\n" +
            $"- **Moderator:** {moderator.Mention}\n" +
            $"- **Reason:** {logEntry.Reason}\n" +
            $"- **Ban Count:** {guildUser.BanCount}",
            ModLogType.Ban
            ));
        await _databaseMethodService.AddInfractionsAsync(
            new Infraction(moderator.Id,targetUser.Id,guild.Id,logEntry.Reason,false,InfractionType.Ban)
            );
        client.Logger.LogInformation(CustomLogEvents.AuditLogManager,"Ban logged for {User} in {Guild} by {ModUser}",targetUser.Username,guild.Name,moderator.Username);
    }
    
    private static ulong CheckIfIdNotNull(string? id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        return ulong.Parse(id);
    }
    private static ulong CheckIfIdNotNull(ulong? id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        return id.Value;
    }

    private class AuditLogEntry
    {
        [JsonProperty("user_id")]
        public ulong? UserId { get; set; }
        [JsonProperty("target_id")]
        public string? TargetId { get; set; }
        [JsonProperty("reason")]
        public string? Reason { get; set; }

        [JsonProperty("id")]
        public ulong AuditLogId { get; set; }
        [JsonProperty("action_type")]
        public AuditLogEvents ActionType { get; set; }
        [JsonProperty("guild_id")]
        public ulong GuildId { get; set; }
        [JsonProperty("changes")]
        public AuditLogChanges[]? Changes { get; set; }
        [JsonProperty("options")]
        public AuditLogOptions? Options { get; set; }
    }

    private class AuditLogChanges
    {
        [JsonProperty("new_value")]
        public object? NewValue { get; set; }
        [JsonProperty("old_value")]
        public object? OldValue { get; set; }

        [JsonProperty("key")] public string Key { get; set; } = null!;
    }

    private class AuditLogOptions
    {
        [JsonProperty("application_id")]
        public ulong ApplicationId { get; set; }
        [JsonProperty("auto_moderation_rule_name")]
        public string AutoModerationRuleName { get; set; }=null!;
        [JsonProperty("auto_moderation_rule_trigger_type")]
        public string AutoModerationRuleTriggerType { get; set; }=null!;
        [JsonProperty("channel_id")]
        public ulong ChannelId { get; set; }
        [JsonProperty("count")]
        public string Count { get; set; } = null!;
        [JsonProperty("delete_member_days")]
        public string DeleteMemberDays { get; set; }=null!;
        [JsonProperty("id")]
        public ulong OverwrittenId { get; set; }
        [JsonProperty("members_removed")]
        public string MembersRemoved { get; set; }=null!;
        [JsonProperty("message_id")]
        public ulong MessageId { get; set; }
        [JsonProperty("role_name")]
        public string RoleName { get; set; }=null!;
        [JsonProperty("type")]
        public string Type { get; set; }=null!;
    }

    private enum AuditLogEvents
    {
        GuildUpdate=1,
        ChannelCreate=10,
        ChannelUpdate=11,
        ChannelDelete=12,
        ChannelOverwriteCreate=13,
        ChannelOverwriteUpdate=14,
        ChannelOverwriteDelete=15,
        MemberKick=20,
        MemberPrune=21,
        MemberBanAdd=22,
        MemberBanRemove=23,
        MemberUpdate=24,
        MemberRoleUpdate=25,
        MemberMove=26,
        MemberDisconnect=27,
        BotAdd=28,
        RoleCreate=30,
        RoleUpdate=31,
        RoleDelete=32,
        InviteCreate=40,
        InviteUpdate=41,
        InviteDelete=42,
        WebhookCreate=50,
        WebhookUpdate=51,
        WebhookDelete=52,
        EmojiCreate=60,
        EmojiUpdate=61,
        EmojiDelete=62,
        MessageDelete=72,
        MessageBulkDelete=73,
        MessagePin=74,
        MessageUnpin=75,
        IntegrationCreate=80,
        IntegrationUpdate=81,
        IntegrationDelete=82,
        StageInstanceCreate=83,
        StageInstanceUpdate=84,
        StageInstanceDelete=85,
        StickerCreate=90,
        StickerUpdate=91,
        StickerDelete=92,
        GuildScheduledEventCreate=100,
        GuildScheduledEventUpdate=101,
        GuildScheduledEventDelete=102,
        ThreadCreate=110,
        ThreadUpdate=111,
        ThreadDelete=112,
        ApplicationCommandPermissionUpdate=121,
        AutoModerationRuleCreate=140,
        AutoModerationRuleUpdate=141,
        AutoModerationRuleDelete=142,
        AutoModerationBlockMessage=143,
        AutoModerationFlagToChannel=144,
        AutoModerationUserCommunicationDisabled=145
    }
}