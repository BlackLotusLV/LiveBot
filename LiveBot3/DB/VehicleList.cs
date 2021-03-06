﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Vehicle_List", Schema = "livebot")]
    public class VehicleList
    {
        [Key]
        [Column("id_vehicle")]
        public int ID_Vehicle { get; set; }

        [Required]
        [Column("discipline")]
        public int Discipline { get; set; }

        [Required]
        [Column("brand")]
        public string Brand { get; set; }

        [Required]
        [Column("model")]
        public string Model { get; set; }

        [Required]
        [Column("year")]
        public string Year { get; set; }

        [Required]
        [Column("type")]
        public string Type { get; set; }

        [Required]
        [Column("summit_vehicle")]
        public bool IsSummitVehicle { get; set; }

        [Required]
        [Column("selected")]
        public bool IsSelected { get; set; }

        [Required]
        [Column("cc_only")]
        public bool IsCCOnly { get; set; }

        [Required]
        [Column("motorpass")]
        public bool IsMotorPassExclusive { get; set; }

        [Required]
        [Column("tier")]
        public char VehicleTier { get; set; }
    }
}