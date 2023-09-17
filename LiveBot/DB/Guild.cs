namespace LiveBot.DB;

public class Guild
{
    public Guild(ulong id)
    {
        Id = id;
    }

    public ulong Id
    {
        get => _id;
        set => _id = Convert.ToUInt64(value);
    }

    private ulong _id;

    public ulong? DeleteLogChannelId
    {
        get => _deleteLogChannelId;
        set => _deleteLogChannelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _deleteLogChannelId;

    public ulong? UserTrafficChannelId
    {
        get => _userTrafficChannelId;
        set => _userTrafficChannelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _userTrafficChannelId;

    public ulong? ModerationLogChannelId
    {
        get => _moderationLogChannelId;
        set => _moderationLogChannelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _moderationLogChannelId;

    public ulong? ModMailChannelId
    {
        get => _modMailChannelId;
        set => _modMailChannelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _modMailChannelId;
    public bool HasLinkProtection { get; set; }

    public ulong? VoiceActivityLogChannelId
    {
        get => _voiceActivityLogChannelId;
        set => _voiceActivityLogChannelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _voiceActivityLogChannelId;

    public ulong? EventLogChannelId
    {
        get => _eventLogChannelId;
        set => _eventLogChannelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _eventLogChannelId;
    public bool HasEveryoneProtection { get; set; }
    public bool ModMailEnabled { get; set; } = false;

    public ulong? WelcomeChannelId
    {
        get => _welcomeChannelId;
        set => _welcomeChannelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _welcomeChannelId;
    public string WelcomeMessage { get; set; }
    public string GoodbyeMessage { get; set; }
    public bool HasScreening { get; set; }

    public ulong? RoleId
    {
        get => _roleId;
        set => _roleId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _roleId;

    public ulong? WhiteListRoleId
    {
        get => _whiteListRoleId;
        set => _whiteListRoleId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    private ulong? _whiteListRoleId;

    public ICollection<GuildUser> GuildUsers { get; set; }
    public ICollection<RankRoles> RankRoles { get; set; }
    public ICollection<ButtonRoles> ButtonRoles { get; set; }
    public ICollection<RoleTagSettings> RoleTagSettings { get; set; }
    public ICollection<StreamNotifications> StreamNotifications { get; set; }
    public ICollection<SpamIgnoreChannels> SpamIgnoreChannels { get; set; }
    public ICollection<WhiteListSettings> WhiteListSettings { get; set; }
    public ICollection<MediaOnlyChannels> MediaOnlyChannels { get; set; }
    public ICollection<PhotoCompSettings> PhotoCompSettings { get; set; }
    public ICollection<Tag> Tags { get; set; }
    public ICollection<VanityWhitelist> WhitelistedVanities { get; set; }
}