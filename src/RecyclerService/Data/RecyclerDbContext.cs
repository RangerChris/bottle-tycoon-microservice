﻿using Microsoft.EntityFrameworkCore;
using RecyclerService.Models;

namespace RecyclerService.Data;

public class RecyclerDbContext : DbContext
{
    public RecyclerDbContext(DbContextOptions<RecyclerDbContext> options) : base(options)
    {
    }

    public DbSet<Recycler> Recyclers { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recycler>(eb =>
        {
            eb.HasKey(r => r.Id);
            eb.Property(r => r.Name).HasMaxLength(200).IsRequired();
            eb.Property(r => r.Capacity).IsRequired();
            eb.Property(r => r.BottleInventoryJson).IsRequired();
            eb.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
            eb.HasMany(r => r.Customers).WithOne(v => v.Recycler).HasForeignKey(v => v.RecyclerId).OnDelete(DeleteBehavior.Restrict);
            eb.HasIndex(r => r.Capacity);
        });

        modelBuilder.Entity<Customer>(eb =>
        {
            eb.HasKey(v => v.Id);
            eb.Property(v => v.BottleCountsJson).IsRequired();
            eb.Property(v => v.CustomerType).HasMaxLength(50);
            eb.Property(v => v.ArrivedAt).HasDefaultValueSql("now()");
        });

        base.OnModelCreating(modelBuilder);
    }
}