﻿using Microsoft.EntityFrameworkCore;

namespace LiveBot.DB
{
    internal class VehicleListContext : DbContext
    {
        public DbSet<VehicleList> VehicleList { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(CustomMethod.GetConnString());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VehicleList>().ToTable("Vehicle_List");
        }
    }
}