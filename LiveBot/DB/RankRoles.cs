using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Rank_Roles", Schema = "livebot")]
    public class RankRoles
    {
        public RankRoles(LiveBotDbContext context)
        {
            ServerSettings serverSettings = context.ServerSettings.FirstOrDefault(x => x.GuildId == this.GuildId);
            if (serverSettings != null) return;
            serverSettings = new ServerSettings() { GuildId = this.GuildId };
            context.ServerSettings.Add(serverSettings);
        }
        [Key]
        [Column("id_rank_roles", TypeName = "serial")]
        public int IdRankRoles { get; set; }

        [Required, Column("server_id"), ForeignKey("Button_Roles_server_id_fkey")]
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Required]
        [Column("role_id")]
        public ulong RoleId
        { 
            get => _roleId;
            set => _roleId = Convert.ToUInt64(value);
        }

        private ulong _roleId;

        [Required]
        [Column("server_rank")]
        public long ServerRank { get; set; }
    }
}