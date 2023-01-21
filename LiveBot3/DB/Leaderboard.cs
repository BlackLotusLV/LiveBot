using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Leaderboard", Schema = "livebot")]
    public class Leaderboard
    {
        public Leaderboard(LiveBotDbContext context)
        {
            Leaderboard leaderboard = context.Leaderboard.FirstOrDefault(x => x.UserDiscordId == this.UserDiscordId);
            if (leaderboard != null) return;
            leaderboard = new Leaderboard(context) { UserDiscordId = this.ParentUserDiscordId };
            context.Leaderboard.Add(leaderboard);
        }
        [Key]
        [Column("id_user")]
        public ulong UserDiscordId
        { 
            get => _userDiscordId; 
            init => _userDiscordId = Convert.ToUInt64(value);
        }
        private readonly ulong _userDiscordId;

        [Required]
        [Column("cookie_given")]
        public int CookiesGiven { get; set; }

        [Required]
        [Column("cookie_taken")]
        public int CookiesTaken { get; set; }

        [Column("cookie_date")]
        public DateTime CookieDate { get; set; }
        [Column("locale")]
        public string Locale { get; set; }

        [ForeignKey("parent_fk")]
        [Column("parent_user_id")]
        public ulong ParentUserDiscordId
        {
            get => _parentUserDiscordId;
            set => _parentUserDiscordId = Convert.ToUInt64(value);
        }
        private ulong _parentUserDiscordId;
        
        public virtual ICollection<UbiInfo> UbiInfo { get; set; }
        public virtual ICollection<Leaderboard> Child { get; set; }
        public virtual ICollection<ServerRanks> ServerRanks { get; set; }
    }
}