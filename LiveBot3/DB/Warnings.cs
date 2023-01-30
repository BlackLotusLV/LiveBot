using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Warnings", Schema = "livebot")]
    public class Warnings
    {
        public Warnings(LiveBotDbContext context, ulong adminDiscordId, ulong userDiscordId, ulong guildId, string reason, bool isActive, string type)
        {
            Reason = reason;
            IsActive = isActive;
            AdminDiscordId = adminDiscordId;
            UserDiscordId = userDiscordId;
            GuildId = guildId;
            Type = type;
            
            ServerRanks serverRanks = context.ServerRanks.FirstOrDefault(x => x.UserDiscordId==userDiscordId && x.GuildId==guildId);
            if (serverRanks != null) return;
            serverRanks = new ServerRanks(context, UserDiscordId, GuildId);
            var item = context.ServerRanks.Add(serverRanks);
            
            ServerRanksId = item.Entity.IdServerRank;
        }
        
        [Key]
        [Column("id_warning")]
        public int IdWarning { get; set; }

        [Required]
        [Column("reason")]
        public string Reason { get; set; }

        [Required]
        [Column("active")]
        public bool IsActive { get; set; }

        [Required]
        [Column("time_created")]
        public DateTime TimeCreated { get; set; }

        [Required]
        [Column("admin_id")]
        public ulong AdminDiscordId
        { 
            get => _adminDiscordId; 
            set => _adminDiscordId = Convert.ToUInt64(value);
        }

        private ulong _adminDiscordId;

        [Required]
        [Column("user_id")]
        public ulong UserDiscordId
        {
            get => _userDiscordId; 
            set => _userDiscordId = Convert.ToUInt64(value);
        }

        private ulong _userDiscordId;

        [Required]
        [Column("server_id")]
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Required]
        [Column("type")]
        public string Type { get; set; }

        [ForeignKey("warnings_fk")]
        [Required]
        [Column("server_ranks_id")]
        public int ServerRanksId { get; set; }
    }
}