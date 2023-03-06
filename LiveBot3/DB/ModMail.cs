namespace LiveBot.DB;

public class ModMail
{
    public ModMail(ulong guildId, ulong userDiscordId, DateTime lastMessageTime, string colorHex, bool isActive =true)
    {
        GuildId = guildId;
        UserDiscordId = userDiscordId;
        LastMessageTime = lastMessageTime;
        ColorHex = colorHex;
        IsActive = isActive;
    }

    private ulong _guildId;
    private ulong _userDiscordId;

    public long Id { get; set; }

    public ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }

    public ulong UserDiscordId
    {
        get => _userDiscordId;
        set => _userDiscordId = Convert.ToUInt64(value);
    }

    public DateTime LastMessageTime { get; set; }
    public bool HasChatted { get; set; }
    public bool IsActive { get; set; }
    public string ColorHex { get; set; }

    public GuildUser GuildUser { get; set; }
}