using FordNexus.Application.Abstractions;
using FordNexus.Domain.Entities;
using FordNexus.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FordNexus.Infrastructure.Persistence;

public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options, IFieldEncryptor encryptor)
    : DbContext(options), INexusDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Dealership> Dealerships => Set<Dealership>();
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

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
            e.Property(x => x.OwnerName).HasConversion(Encrypted("Vehicle.OwnerName"));
            e.Property(x => x.OwnerPhone).HasConversion(Encrypted("Vehicle.OwnerPhone"));
            e.HasMany(x => x.ServiceOrders).WithOne(o => o.Vehicle!).HasForeignKey(o => o.VehicleId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Appointments).WithOne(a => a.Vehicle!).HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ServiceOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(12, 2);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
            e.Property(x => x.Action).HasMaxLength(80).IsRequired();
        });

        b.Entity<Appointment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.IsActive);
        });
    }

    private ValueConverter<string, string> Encrypted(string purpose)
    {
        var fieldEncryptor = encryptor;
        return new ValueConverter<string, string>(
            v => fieldEncryptor.Encrypt(v, purpose),
            v => fieldEncryptor.Decrypt(v, purpose));
    }
}
