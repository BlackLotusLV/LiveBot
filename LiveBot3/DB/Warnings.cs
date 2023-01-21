using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Warnings", Schema = "livebot")]
    public class Warnings
    {
        [Key]
        [Column("id_warning")]
        public int ID_Warning { get; set; }

        [Required]
        [Column("reason")]
        public string Reason { get; set; }

        [Required]
        [Column("active")]
        public bool Active { get; set; }

        [Required]
        [Column("time_created")]
        public DateTime Time_Created { get; set; }

        [Required]
        [Column("admin_id")]
        public ulong Admin_ID
        { get => _Admin_ID; set { _Admin_ID = Convert.ToUInt64(value); } }

        private ulong _Admin_ID;

        [Required]
        [Column("user_id")]
        public ulong User_ID
        { get => _User_ID; set { _User_ID = Convert.ToUInt64(value); } }

        private ulong _User_ID;

        [Required]
        [Column("server_id")]
        public ulong Server_ID
        { get => _serverId; set { _serverId = Convert.ToUInt64(value); } }

        private ulong _serverId;

        [Required]
        [Column("type")]
        public string Type { get; set; }

        [Required]
        [Column("server_ranks_id")]
        public int Server_Ranks_ID { get; set; }
    }
}