namespace LiveBot.DB;

public class RoleTagSettings
{
    private ulong _guildId;
    private ulong _roleId;
    private ulong? _channelId;
    public long Id { get; set; }

    public required ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }

    public required ulong RoleId
    {
        get => _roleId;
        set => _roleId = Convert.ToUInt64(value);
    }

    public ulong? ChannelId
    {
        get => _channelId;
        set => _channelId = value.HasValue ? Convert.ToUInt64(value) : default(ulong?);
    }

    public required int Cooldown { get; set; }
    public required DateTime LastTimeUsed { get; set; }
    public required string Message { get; set; }
    public required string Description { get; set; }

    public Guild Guild { get; set; }
}