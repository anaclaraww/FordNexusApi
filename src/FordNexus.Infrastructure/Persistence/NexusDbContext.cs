using FordNexus.Application.Abstractions;
using FordNexus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Infrastructure.Persistence;

public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options) : DbContext(options), INexusDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Dealership> Dealerships => Set<Dealership>();
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(160).IsRequired();
        });

        b.Entity<Dealership>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Cnpj).IsUnique();
        });

        b.Entity<Workshop>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Cnpj).IsUnique();
            e.Ignore(x => x.IsCertified);
        });

        b.Entity<Vehicle>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Vin).IsUnique();
            e.Property(x => x.Vin).HasMaxLength(17).IsRequired();
            e.HasMany(x => x.ServiceOrders).WithOne(o => o.Vehicle!).HasForeignKey(o => o.VehicleId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Appointments).WithOne(a => a.Vehicle!).HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ServiceOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(12, 2);
        });

        b.Entity<Appointment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.IsActive);
        });
    }
}
