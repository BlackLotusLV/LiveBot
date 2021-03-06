﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Commands_Used_Count", Schema = "livebot")]
    internal class CommandsUsedCount
    {
        [Key]
        [Column("command_id")]
        public int Command_ID { get; set; }

        [Required]
        [Column("command")]
        public string Name { get; set; }

        [Required]
        [Column("used_count")]
        public long Used_Count { get; set; }
    }
}