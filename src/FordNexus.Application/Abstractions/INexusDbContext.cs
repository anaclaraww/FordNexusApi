using FordNexus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Application.Abstractions;

public interface INexusDbContext
{
    DbSet<User> Users { get; }
    DbSet<Dealership> Dealerships { get; }
    DbSet<Workshop> Workshops { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<ServiceOrder> ServiceOrders { get; }
    DbSet<Appointment> Appointments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
