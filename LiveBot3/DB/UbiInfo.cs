using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Ubi_Info", Schema = "livebot")]
    public class UbiInfo
    {
        public UbiInfo(LiveBotDbContext context)
        {
            Leaderboard leaderboard = context.Leaderboard.FirstOrDefault(x => x.UserDiscordId == this.UserDiscordId);
            if (leaderboard != null) return;
            leaderboard = new Leaderboard(context) { UserDiscordId = this.UserDiscordId };
            context.Leaderboard.Add(leaderboard);
        }
        
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [ForeignKey("ubi_info_fk")]
        [Required]
        [Column("discord_id")]
        public ulong UserDiscordId
        {
            get => _userDiscordId; 
            init => _userDiscordId = Convert.ToUInt64(value);
        }

        private readonly ulong _userDiscordId;
        [Required]
        [Column("profile_id")]
        public string ProfileId { get; set; }

        [Required]
        [Column("platform")]
        public string Platform { get; set; }
    }
}
