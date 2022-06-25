using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Ubi_Info", Schema = "livebot")]
    internal class UbiInfo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("discord_id")]
        public ulong Discord_Id 
        { get => _Discord_Id; set { _Discord_Id = Convert.ToUInt64(value); } }

        private ulong _Discord_Id;
        [Required]
        [Column("profile_id")]
        public string Profile_Id { get; set; }

        [Required]
        [Column("platform")]
        public string Platform { get; set; }
    }
}
