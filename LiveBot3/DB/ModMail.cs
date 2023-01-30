using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Mod_Mail", Schema = "livebot")]
    public class ModMail
    {
        public ModMail(LiveBotDbContext context, ulong guildId, ulong userDiscordId, DateTime lastMessageTime, string colorHex)
        {
            GuildId = guildId;
            UserDiscordId = userDiscordId;
            LastMessageTime = lastMessageTime;
            ColorHex = colorHex;
            
            ServerRanks serverRanks = context.ServerRanks.FirstOrDefault(x => x.UserDiscordId==userDiscordId && x.GuildId==guildId);
            if (serverRanks != null) return;
            serverRanks = new ServerRanks(context, UserDiscordId,GuildId);
            var item = context.ServerRanks.Add(serverRanks);
            this.ServerRanksId = item.Entity.IdServerRank;
        }
        [Key]
        [Column("id_modmail")]
        public long ModMailId { get; set; }

        [Required]
        [Column("server_id")]
        public ulong GuildId
        { 
            get => _guildId;
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Required]
        [Column("user_id")]
        public ulong UserDiscordId
        { 
            get => _userDiscordId;
            set => _userDiscordId = Convert.ToUInt64(value);
        }

        private ulong _userDiscordId;

        [Required]
        [Column("last_message_time")]
        public DateTime LastMessageTime { get; set; }

        [Required]
        [Column("has_chatted")]
        public bool HasChatted { get; set; }

        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; }

        [Required]
        [Column("color_hex")]
        public string ColorHex { get; set; }

        [ForeignKey("mod_mail_fk")]
        [Required]
        [Column("server_ranks_id")]
        public int ServerRanksId { get; set; }
    }
}