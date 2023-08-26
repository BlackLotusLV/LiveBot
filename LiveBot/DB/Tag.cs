namespace LiveBot.DB;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Content { get; set; }
    public required ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }
    private ulong _guildId;
    public Guild Guild { get; set; }
    public required ulong OwnerId
    {
        get => _ownerId;
        set => _ownerId = Convert.ToUInt64(value);
    }
    private ulong _ownerId;
    public User Owner { get; set; }
}