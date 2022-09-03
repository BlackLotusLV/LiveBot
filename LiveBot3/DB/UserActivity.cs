using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("User_Activity", Schema = "livebot")]
    internal class UserActivity
    {
        [Key]
        [Column("id_user_activity")]
        public long ID_User_Activity { get; set; }

        [Required]
        [Column("user_id")]
        public ulong User_ID
        { get => _User_ID; set { _User_ID = Convert.ToUInt64(value); } }
        private ulong _User_ID;
        [Required]
        [Column("guild_id")]
        public ulong Guild_ID
        { get => _Guild_ID; set { _Guild_ID = Convert.ToUInt64(value); } }
        private ulong _Guild_ID;

        [Required]
        [Column("points")]
        public int Points { get; set; }

        [Required]
        [Column("date")]
        public DateTime Date { get =>_Date; set { _Date = DateTime.SpecifyKind(value, DateTimeKind.Utc); } }
        private DateTime _Date;

        [Required]
        [Column("server_ranks_id")]
        public int Server_Ranks_ID { get; set; }

        public UserActivity(ulong user_ID, ulong guild_ID, int points, DateTime date)
        {
            User_ID = user_ID;
            Guild_ID = guild_ID;
            Points = points;
            Date = date;
        }
    }
}