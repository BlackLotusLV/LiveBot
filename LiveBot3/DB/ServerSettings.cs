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

        [Required, Column("delete_log")]
        public ulong DeleteLogChannelId
        { 
            get => _deleteLogChannelId; 
            set => _deleteLogChannelId = Convert.ToUInt64(value);
        }

        private ulong _deleteLogChannelId;

        [Required, Column("user_traffic")]
        public ulong UserTrafficChannelId
        { 
            get => _userTrafficChannelId; 
            set => _userTrafficChannelId = Convert.ToUInt64(value);
        }

        private ulong _userTrafficChannelId;

        [Required, Column("wkb_log")]
        public ulong ModerationLogChannelId
        { 
            get => _moderationLogChannelId; 
            set => _moderationLogChannelId = Convert.ToUInt64(value);
        }

        private ulong _moderationLogChannelId;

        [Required, Column("spam_exception")]
        public ulong[] SpamExceptionChannels
        { 
            get => _spamExceptionChannels;
            set => _spamExceptionChannels = value.Select(Convert.ToUInt64).ToArray();
        }

        private ulong[] _spamExceptionChannels;

        [Required, Column("mod_mail")]
        public ulong ModMailChannelId
        { get => _modMailChannelId; set => _modMailChannelId = Convert.ToUInt64(value);
        }

        private ulong _modMailChannelId;

        [Required, Column("has_link_protection")]
        public bool HasLinkProtection { get; set; }

        [Required, Column("voice_activity_log")]
        public ulong VoiceActivityLogChannelId
        { 
            get => _voiceActivityLogChannelId;
            set => _voiceActivityLogChannelId = Convert.ToUInt64(value);
        }

        private ulong _voiceActivityLogChannelId;

        [Required, Column("event_log")]
        public ulong EventLogChannelId
        { 
            get => _eventLogChannelId; 
            set => _eventLogChannelId = Convert.ToUInt64(value);
        }

        private ulong _eventLogChannelId;

        [Required, Column("has_everyone_protection")]
        public bool HasEveryoneProtection { get; set; }

        [Required, Column("mod_mail_enabled")]
        public bool ModMailEnabled { get; set; } = false;
        
        public virtual ServerWelcomeSettings WelcomeSettings { get; set; }
        public virtual ICollection<ServerRanks> ServerRanks { get; set; }
        public virtual ICollection<RankRoles> RankRoles { get; set; }
        public virtual ICollection<ButtonRoles> ButtonRoles { get; set; }
        public virtual ICollection<RoleTagSettings> RoleTagSettings { get; set; }
        public virtual ICollection<StreamNotifications> StreamNotifications { get; set; }
    }
}