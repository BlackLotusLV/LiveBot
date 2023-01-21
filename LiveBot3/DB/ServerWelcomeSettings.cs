using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Server_Welcome_Settings", Schema = "livebot")]
    public class ServerWelcomeSettings
    {
        [Key,ForeignKey("server_welcome_settings_fk"),Column("server_id")]
        public ulong GuildId
        { 
            get => _guildId;
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Required,Column("channel_id")]
        public ulong ChannelId
        { 
            get => _channelId; 
            set => _channelId = Convert.ToUInt64(value);
        }

        private ulong _channelId;

        [Column("welcome_msg")]
        public string WelcomeMessage { get; set; }

        [Column("goodbye_msg")]
        public string GoodbyeMessage { get; set; }

        [Required]
        [Column("has_screening")]
        public bool HasScreening { get; set; }

        [Required]
        [Column("role")]
        public ulong RoleId
        { 
            get => _roleId; 
            set => _roleId = Convert.ToUInt64(value);
        }

        private ulong _roleId;
        
        [Column("whitelist_role")]
        public ulong? WhiteListRole
        { 
            get => _whiteListRole; 
            set => _whiteListRole = Convert.ToUInt64(value);
        }

        private ulong _whiteListRole;
    }
}