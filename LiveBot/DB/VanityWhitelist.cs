namespace LiveBot.DB;

public class VanityWhitelist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }
    private ulong _guildId;
    public Guild Guild { get; set; }
    public required string VanityCode { get; set; }
}