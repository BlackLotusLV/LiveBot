using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LiveBot.Automation;

namespace LiveBot.DB
{
    [Table("Server_Settings", Schema = "livebot")]
    public class ServerSettings
    {
        [Key, Column("id_server")]
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Column("delete_log")]
        public ulong? DeleteLogChannelId
        { 
            get => _deleteLogChannelId; 
            set => _deleteLogChannelId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }

        private ulong? _deleteLogChannelId;

        [Column("user_traffic")]
        public ulong? UserTrafficChannelId
        { 
            get => _userTrafficChannelId; 
            set => _userTrafficChannelId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }

        private ulong? _userTrafficChannelId;

        [Column("wkb_log")]
        public ulong? ModerationLogChannelId
        { 
            get => _moderationLogChannelId; 
            set => _moderationLogChannelId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }

        private ulong? _moderationLogChannelId;

        [Column("mod_mail")]
        public ulong? ModMailChannelId
        { get => _modMailChannelId; set => _modMailChannelId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }

        private ulong? _modMailChannelId;

        [Required, Column("has_link_protection")]
        public bool HasLinkProtection { get; set; }

        [Column("voice_activity_log")]
        public ulong? VoiceActivityLogChannelId
        { 
            get => _voiceActivityLogChannelId;
            set => _voiceActivityLogChannelId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }

        private ulong? _voiceActivityLogChannelId;

        [Column("event_log")]
        public ulong? EventLogChannelId
        { 
            get => _eventLogChannelId; 
            set => _eventLogChannelId = value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }

        private ulong? _eventLogChannelId;

        [Required, Column("has_everyone_protection")]
        public bool HasEveryoneProtection { get; set; }

        [Required, Column("mod_mail_enabled")]
        public bool ModMailEnabled { get; set; } = false;

        [Column("welcome_channel")]
        public ulong? WelcomeChannelId
        {
            get => _welcomeChannelId;
            set => _welcomeChannelId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }
        private ulong? _welcomeChannelId;
        
        [Column("welcome_msg")]
        public string WelcomeMessage { get; set; }

        [Column("goodbye_msg")]
        public string GoodbyeMessage { get; set; }

        [Required]
        [Column("has_screening")]
        public bool HasScreening { get; set; }
        
        [Column("join_role")]
        public ulong? RoleId
        {
            get => _roleId; 
            set => _roleId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }

        private ulong? _roleId;
        
        [Column("whitelist_role")]
        public ulong? WhiteListRoleId
        { 
            get => _whiteListRoleId; 
            set => _whiteListRoleId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }
        private ulong? _whiteListRoleId;
        
        public virtual ICollection<ServerRanks> ServerRanks { get; set; }
        public virtual ICollection<RankRoles> RankRoles { get; set; }
        public virtual ICollection<ButtonRoles> ButtonRoles { get; set; }
        public virtual ICollection<RoleTagSettings> RoleTagSettings { get; set; }
        public virtual ICollection<StreamNotifications> StreamNotifications { get; set; }
        public virtual ICollection<SpamIgnoreChannels> SpamIgnoreChannels { get; set; }
    }
}