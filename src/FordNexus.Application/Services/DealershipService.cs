using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using FordNexus.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Application.Services;

public interface IDealershipService
{
    Task<IReadOnlyList<DealershipResponse>> ListAsync(CancellationToken ct);
    Task<DealershipResponse> GetAsync(Guid id, CancellationToken ct);
    Task<MaintenanceQueueResponse> GetMaintenanceQueueAsync(Guid id, int limit, CancellationToken ct);
}

public sealed class DealershipService(INexusDbContext db, ICurrentUser currentUser, TimeProvider time) : IDealershipService
{
    // regra do manual do proprietário mesmo, não precisa de ML pra saber que tá na hora da revisão
    public const int RevisionIntervalKm = 10_000;
    public const int RevisionIntervalMonths = 12;
    private const double DueThreshold = 0.9;

    public async Task<IReadOnlyList<DealershipResponse>> ListAsync(CancellationToken ct) =>
        (await db.Dealerships.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct))
        .Select(d => d.ToResponse()).ToList();

    public async Task<DealershipResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await db.Dealerships.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct)
         ?? throw new NotFoundException($"Concessionária {id} não encontrada.")).ToResponse();

    public async Task<MaintenanceQueueResponse> GetMaintenanceQueueAsync(Guid id, int limit, CancellationToken ct)
    {
        if (!await db.Dealerships.AnyAsync(d => d.Id == id, ct))
            throw new NotFoundException($"Concessionária {id} não encontrada.");

        if (currentUser.IsInRole(Roles.Dealer) && currentUser.DealershipId != id)
            throw new ForbiddenException("Concessionária só acessa a fila da própria carteira.");

        var now = time.GetUtcNow().UtcDateTime;
        var vehicles = await db.Vehicles.AsNoTracking()
            .Include(v => v.ServiceOrders)
            .Include(v => v.Appointments)
            .Where(v => v.HomeDealershipId == id && v.ContactConsent)
            .ToListAsync(ct);

        var due = new List<MaintenanceQueueItem>();
        foreach (var v in vehicles)
        {
            if (v.Appointments.Any(a => a.IsActive)) continue;

            var last = v.ServiceOrders.OrderByDescending(o => o.PerformedAt).FirstOrDefault();
            var kmSince = last is null ? v.CurrentMileage : Math.Max(0, v.CurrentMileage - last.Mileage);
            double? monthsSince = last is null ? null : Math.Round((now - last.PerformedAt).TotalDays / 30.44, 1);

            var kmRatio = kmSince / (double)RevisionIntervalKm;
            var timeRatio = monthsSince is null ? 1.5 : monthsSince.Value / RevisionIntervalMonths;
            var urgency = Math.Max(kmRatio, timeRatio);
            if (urgency < DueThreshold) continue;

            var reasons = new List<string>();
            if (last is null) reasons.Add("Nenhum serviço registrado na rede Ford.");
            if (kmRatio >= DueThreshold) reasons.Add($"{kmSince:N0} km desde o último serviço (intervalo: {RevisionIntervalKm:N0} km).");
            if (monthsSince is not null && timeRatio >= DueThreshold)
                reasons.Add($"{monthsSince.Value:N1} meses desde o último serviço (intervalo: {RevisionIntervalMonths} meses).");

            due.Add(new MaintenanceQueueItem(
                v.Vin, v.Model, v.ModelYear, v.OwnerName, v.OwnerPhone,
                last?.PerformedAt, kmSince, monthsSince,
                (int)Math.Round(urgency * 100),
                "Revision",
                reasons));
        }

        var ordered = due.OrderByDescending(i => i.UrgencyScore).ToList();
        return new MaintenanceQueueResponse(id, now, ordered.Count, ordered.Take(limit).ToList());
    }
}
