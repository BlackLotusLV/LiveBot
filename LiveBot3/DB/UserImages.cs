﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("User_Images", Schema = "livebot")]
    internal class UserImages
    {
        [Key]
        [Column("id_user_images")]
        public int ID_User_Images { get; set; }
        [Column("user_id")]
        public string User_ID { get; set; }
        [Column("bg_id")]
        public int BG_ID { get; set; }
    }
}