using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Leaderboard", Schema = "livebot")]
    public sealed class Leaderboard
    {
        public Leaderboard(LiveBotDbContext context, ulong userDiscordId)
        {
            UserDiscordId = userDiscordId;
            if (ParentUserDiscordId==null)return;
            Leaderboard entry = context.Leaderboard.FirstOrDefault(x => x.UserDiscordId == ParentUserDiscordId);
            if (entry != null) return;
            entry = new Leaderboard(context, ParentUserDiscordId.Value);
            context.Leaderboard.Add(entry);
            context.SaveChanges();
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
        public ulong? ParentUserDiscordId
        {
            get => _parentUserDiscordId;
            set => _parentUserDiscordId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }
        private ulong? _parentUserDiscordId;
        
        public ICollection<UbiInfo> UbiInfo { get; set; }
        public ICollection<Leaderboard> Child { get; set; }
        public ICollection<ServerRanks> ServerRanks { get; set; }
    }
}