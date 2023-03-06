
namespace LiveBot.DB;

public sealed class GuildUser
{
    public GuildUser(ulong userDiscordId, ulong guildId)
    {
        UserDiscordId = userDiscordId;
        GuildId = guildId;
    }

    public ulong UserDiscordId
    {
        get => _userDiscordId;
        set => _userDiscordId = Convert.ToUInt64(value);
    }

    private ulong _userDiscordId;

    public ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }

    private ulong _guildId;
    public int KickCount { get; set; }
    public int BanCount { get; set; }
    public bool IsModMailBlocked { get; set; }

    public User User { get; set; }
    public Guild Guild { get; set; }

    public ICollection<ModMail> ModMails { get; set; }
    public ICollection<UserActivity> UserActivity { get; set; }
    public ICollection<Infraction> Infractions { get; set; }
}