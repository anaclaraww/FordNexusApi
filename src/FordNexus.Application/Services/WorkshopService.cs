using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using FordNexus.Domain.Common;
using FordNexus.Domain.Entities;
using FordNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Application.Services;

public interface IWorkshopService
{
    Task<PagedResponse<WorkshopResponse>> ListAsync(WorkshopQuery query, CancellationToken ct);
    Task<WorkshopResponse> GetAsync(Guid id, CancellationToken ct);
    Task<WorkshopResponse> CreateAsync(CreateWorkshopRequest request, CancellationToken ct);
    Task<WorkshopResponse> UpdateCertificationAsync(Guid id, UpdateCertificationRequest request, CancellationToken ct);
}

public sealed class WorkshopService(INexusDbContext db, ICurrentUser currentUser, TimeProvider time, IAuditLog audit) : IWorkshopService
{
    public async Task<PagedResponse<WorkshopResponse>> ListAsync(WorkshopQuery query, CancellationToken ct)
    {
        var q = db.Workshops.AsNoTracking();

        if (currentUser.IsInRole(Roles.Admin))
        {
            if (query.Status is not null) q = q.Where(w => w.Status == query.Status);
        }
        else
        {
            q = q.Where(w => w.Status == CertificationStatus.Certified);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            q = q.Where(w => w.City.ToLower() == city);
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(w => w.Name)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);
        return new PagedResponse<WorkshopResponse>(items.Select(w => w.ToResponse()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<WorkshopResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var workshop = await db.Workshops.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workshop is null || (!workshop.IsCertified && !currentUser.IsInRole(Roles.Admin)))
            throw new NotFoundException($"Oficina {id} não encontrada.");
        return workshop.ToResponse();
    }

    public async Task<WorkshopResponse> CreateAsync(CreateWorkshopRequest request, CancellationToken ct)
    {
        if (await db.Workshops.AnyAsync(w => w.Cnpj == request.Cnpj, ct))
            throw new ConflictException($"Já existe uma oficina com o CNPJ {request.Cnpj}.");

        if (request.DistributorDealershipId is not null &&
            !await db.Dealerships.AnyAsync(d => d.Id == request.DistributorDealershipId, ct))
            throw new BusinessRuleException($"Concessionária distribuidora {request.DistributorDealershipId} não existe.");

        var workshop = new Workshop
        {
            Name = request.Name.Trim(),
            Cnpj = request.Cnpj,
            City = request.City.Trim(),
            State = request.State,
            DistributorDealershipId = request.DistributorDealershipId,
            Status = CertificationStatus.Pending
        };
        db.Workshops.Add(workshop);
        audit.Record("workshop.created", "workshop", workshop.Id.ToString(), workshop.Name);
        await db.SaveChangesAsync(ct);
        return workshop.ToResponse();
    }

    public async Task<WorkshopResponse> UpdateCertificationAsync(Guid id, UpdateCertificationRequest request, CancellationToken ct)
    {
        var workshop = await db.Workshops.FirstOrDefaultAsync(w => w.Id == id, ct)
                       ?? throw new NotFoundException($"Oficina {id} não encontrada.");
        var previous = workshop.Status;
        workshop.ChangeCertification(request.Status!.Value, time.GetUtcNow().UtcDateTime);
        if (previous != workshop.Status)
            audit.Record("workshop.certification_changed", "workshop", workshop.Id.ToString(), $"{previous}->{workshop.Status}");
        await db.SaveChangesAsync(ct);
        return workshop.ToResponse();
    }
}
