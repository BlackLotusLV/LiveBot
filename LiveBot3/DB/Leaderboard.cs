﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Leaderboard", Schema = "livebot")]
    internal class Leaderboard
    {
        [Key]
        [Column("id_user")]
        public decimal ID_User { get; set; }

        [Required]
        [Column("followers")]
        public long Followers { get; set; }

        [Required]
        [Column("level")]
        public int Level { get; set; }

        [Required]
        [Column("bucks")]
        public long Bucks { get; set; }

        [Column("daily_used")]
        public string Daily_Used { get; set; }

        [Required]
        [Column("cookie_given")]
        public int Cookies_Given { get; set; }

        [Required]
        [Column("cookie_taken")]
        public int Cookies_Taken { get; set; }

        [Column("cookie_used")]
        public string Cookies_Used { get; set; }
    }
}