﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Discipline_List", Schema = "livebot")]
    public class DisciplineList
    {
        [Key]
        [Column("id_discipline")]
        public int ID_Discipline { get; set; }
        [Column("family")]
        public string Family { get; set; }
        [Column("discipline_name")]
        public string Discipline_Name { get; set; }
    }
}