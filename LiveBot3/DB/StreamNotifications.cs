using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Stream_Notification", Schema = "livebot")]
    public class StreamNotifications
    {
        [Key]
        [Column("stream_notification_id")]
        public int StreamNotificationId { get; set; }

        [Required,Column("server_id"),ForeignKey("Stream_Notification_server_id_fkey")]
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Column("games")]
        public string[] Games { get; set; }

        [Column("roles_id")]
        public ulong[] RoleIds
        { 
            get => _roleIds; 
            set => _roleIds = value.Select(Convert.ToUInt64).ToArray();
        }

        private ulong[] _roleIds;

        [Required]
        [Column("channel_id")]
        public ulong ChannelId
        { 
            get => _channelId;
            set => _channelId = Convert.ToUInt64(value);
        }

        private ulong _channelId;
    }
}