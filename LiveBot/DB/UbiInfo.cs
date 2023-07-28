
namespace LiveBot.DB;

public class UbiInfo
{
    public UbiInfo(ulong userDiscordId)
    {
        UserDiscordId = userDiscordId;
    }

    public int Id { get; set; }

    public ulong UserDiscordId
    {
        get => _userDiscordId;
        init => _userDiscordId = Convert.ToUInt64(value);
    }

    private readonly ulong _userDiscordId;
    public string ProfileId { get; set; }
    public string Platform { get; set; }

    public User User { get; set; }
}
