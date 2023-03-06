namespace LiveBot.DB;

public class WhiteListSettings
{
    public WhiteListSettings(ulong guildId, ulong roleId)
    {
        GuildId = guildId;
        RoleId = roleId;
    }
    public int Id { get; set; }
    private ulong _guildId;

    public ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }

    private ulong _roleId;

    public ulong RoleId
    {
        get => _roleId;
        set => _roleId = Convert.ToUInt64(value);
    }
    public ICollection<WhiteList> WhitelistedUsers { get; set; }
    public Guild Guild { get; set; }
}