namespace LiveBot.DB;

public class MediaOnlyChannels
{
    public required ulong ChannelId
    {
        get => _channelId;
        set => _channelId = Convert.ToUInt64(value);
    }
    private ulong _channelId;
    public required ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }
    private ulong _guildId;
    public string ResponseMessage { get; set; }
    
    public Guild Guild { get; set; }
}