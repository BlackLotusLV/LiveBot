﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Warnings", Schema = "livebot")]
    internal class Warnings
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
        [Column("date")]
        public string Date { get; set; }

        [Required]
        [Column("admin_id")]
        public string Admin_ID { get; set; }

        [Required]
        [Column("user_id")]
        public string User_ID { get; set; }

        [Required]
        [Column("server_id")]
        public string Server_ID { get; set; }
    }
}