using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Role_Tag_Settings", Schema = "livebot")]
    public class RoleTagSettings
    {
        public RoleTagSettings(LiveBotDbContext context)
        {
            ServerSettings serverSettings = context.ServerSettings.FirstOrDefault(x => x.GuildId == this.GuildId);
            if (serverSettings != null) return;
            serverSettings = new ServerSettings() { GuildId = this.GuildId };
            context.ServerSettings.Add(serverSettings);
        }
        [Key]
        [Column("id_role_tag")]
        public long Id { get; set; }

        [Required]
        [Column("server_id")]
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Required, Column("role_id"),ForeignKey("roletagsettings_fk")]
        public ulong RoleId
        { 
            get => _roleId;
            set => _roleId = Convert.ToUInt64(value);
        }

        private ulong _roleId;

        [Required]
        [Column("channel_id")]
        public ulong ChannelId
        { 
            get => _channelId; 
            set => _channelId = Convert.ToUInt64(value);
        }

        private ulong _channelId;

        [Required]
        [Column("cooldown_minutes")]
        public int Cooldown { get; set; }

        [Required]
        [Column("last_used")]
        public DateTime LastTimeUsed { get; set; }

        [Required]
        [Column("emoji_id")]
        public ulong EmojiId
        { 
            get => _emojiId;
            set => _emojiId = Convert.ToUInt64(value);
        }

        private ulong _emojiId;

        [Required]
        [Column("message")]
        public string Message { get; set; }

        [Column("description")]
        public string Description { get; set; }
    }
}