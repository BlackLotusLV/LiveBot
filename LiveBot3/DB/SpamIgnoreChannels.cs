using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB;

[Table("spam_ignore_channels", Schema = "livebot")]
public class SpamIgnoreChannels
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("guild_id"),ForeignKey("spam_ignore_channels_fk")]
    public ulong GuildId
    {
        get => _guildId;
        set => _guildId = Convert.ToUInt64(value);
    }

    private ulong _guildId;

    [Column("channel_id")]
    public ulong ChannelId
    {
        get => _channelId;
        set => _channelId = Convert.ToUInt64(value);
    }

    private ulong _channelId;
}