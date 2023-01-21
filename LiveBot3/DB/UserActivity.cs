using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("User_Activity", Schema = "livebot")]
    public class UserActivity
    {
        [Key]
        [Column("id_user_activity")]
        public long IdUserActivity { get; set; }

        [Required]
        [Column("user_id")]
        public ulong UserDiscordId
        { 
            get => _userDiscordId; 
            set => _userDiscordId = Convert.ToUInt64(value);
        }
        private ulong _userDiscordId;
        [Required]
        [Column("guild_id")]
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }
        private ulong _guildId;

        [Required]
        [Column("points")]
        public int Points { get; set; }

        [Required]
        [Column("date")]
        public DateTime Date
        { 
            get =>_date; 
            set => _date = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        private DateTime _date;

        [ForeignKey("user_activity_fk")]
        [Required]
        [Column("server_ranks_id")]
        public int ServerRanksId { get; set; }

        public UserActivity(LiveBotDbContext context, ulong userDiscordId, ulong guildId, int points, DateTime date)
        {
            UserDiscordId = userDiscordId;
            GuildId = guildId;
            Points = points;
            Date = date;
            
            ServerRanks serverRanks = context.ServerRanks.FirstOrDefault(x => x.UserDiscordId==userDiscordId && x.GuildId==guildId);
            if (serverRanks != null) return;
            serverRanks = new ServerRanks(context) { UserDiscordId = this.UserDiscordId, GuildId = this.GuildId};
            var item = context.ServerRanks.Add(serverRanks);
            this.ServerRanksId = item.Entity.IdServerRank;
        }
    }
}