using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB;

public class SpamIgnoreChannels
{
    public SpamIgnoreChannels(ulong guildId, ulong channelId)
    {
        GuildId = guildId;
        ChannelId = channelId;
    }
    private ulong _guildId;
    private ulong _channelId;
    public int Id { get; set; }
    public ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }
    public ulong ChannelId
    {
        get => _channelId;
        set => _channelId = Convert.ToUInt64(value);
    }
    public Guild Guild { get; set; }
}