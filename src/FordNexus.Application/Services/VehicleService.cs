using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using FordNexus.Domain.Common;
using FordNexus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Application.Services;

public interface IVehicleService
{
    Task<PagedResponse<VehicleResponse>> ListAsync(VehicleQuery query, CancellationToken ct);
    Task<VehicleResponse> GetByVinAsync(string vin, CancellationToken ct);
    Task<VehicleResponse> CreateAsync(CreateVehicleRequest request, CancellationToken ct);
    Task<VehicleResponse> UpdateAsync(string vin, UpdateVehicleRequest request, CancellationToken ct);
    Task DeleteAsync(string vin, CancellationToken ct);
    Task<VehicleHistoryResponse> GetHistoryAsync(string vin, CancellationToken ct);
    Task<ServiceOrderResponse> AddServiceOrderAsync(string vin, CreateServiceOrderRequest request, CancellationToken ct);
    Task<ServiceOrderResponse> GetServiceOrderAsync(string vin, Guid id, CancellationToken ct);
}

public sealed class VehicleService(INexusDbContext db, ICurrentUser currentUser, TimeProvider time, IAuditLog audit) : IVehicleService
{
    public async Task<PagedResponse<VehicleResponse>> ListAsync(VehicleQuery query, CancellationToken ct)
    {
        var q = db.Vehicles.AsNoTracking();

        if (currentUser.IsInRole(Roles.Dealer))
            q = q.Where(v => v.HomeDealershipId == currentUser.DealershipId);

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            var model = query.Model.Trim().ToLower();
            q = q.Where(v => v.Model.ToLower().Contains(model));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(v => v.Vin)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<VehicleResponse>(items.Select(v => v.ToResponse()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<VehicleResponse> GetByVinAsync(string vin, CancellationToken ct) =>
        (await FindAsync(vin, tracking: false, ct)).ToResponse();

    public async Task<VehicleResponse> CreateAsync(CreateVehicleRequest request, CancellationToken ct)
    {
        var vin = Vin.Normalize(request.Vin);
        if (await db.Vehicles.AnyAsync(v => v.Vin == vin, ct))
            throw new ConflictException($"Já existe um veículo cadastrado com o VIN {vin}.");

        Guid homeDealershipId;
        if (currentUser.IsInRole(Roles.Dealer))
        {
            homeDealershipId = currentUser.DealershipId
                ?? throw new ForbiddenException("Token de concessionária sem dealership_id.");
        }
        else
        {
            homeDealershipId = request.HomeDealershipId
                ?? throw new ValidationException(nameof(request.HomeDealershipId), "Obrigatório quando o cadastro é feito pelo Admin.");
            if (!await db.Dealerships.AnyAsync(d => d.Id == homeDealershipId, ct))
                throw new BusinessRuleException($"Concessionária {homeDealershipId} não existe.");
        }

        var vehicle = new Vehicle
        {
            Vin = vin,
            Model = request.Model.Trim(),
            ModelYear = request.ModelYear,
            CurrentMileage = request.CurrentMileage,
            OwnerName = request.OwnerName.Trim(),
            OwnerPhone = request.OwnerPhone,
            ContactConsent = request.ContactConsent,
            DataSharingConsent = request.DataSharingConsent,
            TelemetryConsent = request.TelemetryConsent,
            HomeDealershipId = homeDealershipId,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        return vehicle.ToResponse();
    }

    public async Task<VehicleResponse> UpdateAsync(string vin, UpdateVehicleRequest request, CancellationToken ct)
    {
        var vehicle = await FindAsync(vin, tracking: true, ct);
        EnsureDealerOwns(vehicle);

        if (request.CurrentMileage < vehicle.CurrentMileage)
            throw new BusinessRuleException(
                $"A quilometragem não pode diminuir (atual: {vehicle.CurrentMileage} km, informada: {request.CurrentMileage} km).");

        vehicle.OwnerName = request.OwnerName.Trim();
        vehicle.OwnerPhone = request.OwnerPhone;
        vehicle.CurrentMileage = request.CurrentMileage;
        var consentChanges = new List<string>();
        if (vehicle.ContactConsent != request.ContactConsent) consentChanges.Add($"contato:{vehicle.ContactConsent}->{request.ContactConsent}");
        if (vehicle.DataSharingConsent != request.DataSharingConsent) consentChanges.Add($"compartilhamento:{vehicle.DataSharingConsent}->{request.DataSharingConsent}");
        if (vehicle.TelemetryConsent != request.TelemetryConsent) consentChanges.Add($"telemetria:{vehicle.TelemetryConsent}->{request.TelemetryConsent}");

        vehicle.ContactConsent = request.ContactConsent;
        vehicle.DataSharingConsent = request.DataSharingConsent;
        vehicle.TelemetryConsent = request.TelemetryConsent;
        if (consentChanges.Count > 0)
            audit.Record("vehicle.consent_changed", "vehicle", vehicle.Vin, string.Join("; ", consentChanges));
        await db.SaveChangesAsync(ct);
        return vehicle.ToResponse();
    }

    public async Task DeleteAsync(string vin, CancellationToken ct)
    {
        var normalized = Vin.Normalize(vin);
        var vehicle = await db.Vehicles
            .Include(v => v.ServiceOrders)
            .Include(v => v.Appointments)
            .FirstOrDefaultAsync(v => v.Vin == normalized, ct)
            ?? throw new NotFoundException($"Veículo com VIN {normalized} não encontrado.");

        db.Vehicles.Remove(vehicle);
        audit.Record("vehicle.deleted", "vehicle", vehicle.Vin,
            $"{vehicle.ServiceOrders.Count} ordens e {vehicle.Appointments.Count} agendamentos removidos");
        await db.SaveChangesAsync(ct);
    }

    public async Task<VehicleHistoryResponse> GetHistoryAsync(string vin, CancellationToken ct)
    {
        var normalized = Vin.Normalize(vin);
        var vehicle = await db.Vehicles.AsNoTracking()
            .Include(v => v.ServiceOrders)
            .FirstOrDefaultAsync(v => v.Vin == normalized, ct)
            ?? throw new NotFoundException($"Veículo com VIN {normalized} não encontrado.");

        if (currentUser.IsInRole(Roles.Partner) && !vehicle.DataSharingConsent)
            throw new ForbiddenException("O proprietário não autorizou o compartilhamento do histórico deste veículo com parceiros.");

        if (currentUser.IsInRole(Roles.Partner))
        {
            audit.Record("vehicle.history_shared", "vehicle", vehicle.Vin, "histórico consultado por parceiro");
            await db.SaveChangesAsync(ct);
        }

        var timeline = vehicle.ServiceOrders
            .OrderByDescending(o => o.PerformedAt)
            .Select(o => o.ToResponse(vehicle.Vin))
            .ToList();

        var total = vehicle.ServiceOrders.Count;
        var genuineRate = total == 0 ? 0 : Math.Round(vehicle.ServiceOrders.Count(o => o.GenuineParts) / (double)total, 2);

        return new VehicleHistoryResponse(
            vehicle.Vin, vehicle.Model, vehicle.ModelYear, vehicle.CurrentMileage,
            total, vehicle.ServiceOrders.Sum(o => o.Amount), genuineRate,
            timeline.FirstOrDefault()?.PerformedAt, timeline);
    }

    public async Task<ServiceOrderResponse> AddServiceOrderAsync(string vin, CreateServiceOrderRequest request, CancellationToken ct)
    {
        var normalized = Vin.Normalize(vin);
        var vehicle = await db.Vehicles
            .Include(v => v.ServiceOrders)
            .FirstOrDefaultAsync(v => v.Vin == normalized, ct)
            ?? throw new NotFoundException($"Veículo com VIN {normalized} não encontrado.");

        var order = new ServiceOrder
        {
            VehicleId = vehicle.Id,
            ServiceType = request.ServiceType!.Value,
            Description = request.Description.Trim(),
            Mileage = request.Mileage,
            Amount = request.Amount,
            GenuineParts = request.GenuineParts,
            RegisteredAt = time.GetUtcNow().UtcDateTime
        };

        // o local sai do token e não do body, senão uma loja conseguiria registrar serviço no nome de outra
        if (currentUser.IsInRole(Roles.Workshop))
        {
            var workshop = await db.Workshops.AsNoTracking().FirstOrDefaultAsync(w => w.Id == currentUser.WorkshopId, ct)
                           ?? throw new ForbiddenException("Oficina do token não encontrada.");
            if (!workshop.IsCertified)
                throw new ForbiddenException("Apenas oficinas certificadas Ford Service Partner podem registrar ordens de serviço.");
            order.WorkshopId = workshop.Id;
        }
        else if (currentUser.IsInRole(Roles.Dealer))
        {
            order.DealershipId = currentUser.DealershipId
                ?? throw new ForbiddenException("Token de concessionária sem dealership_id.");
        }
        else
        {
            throw new ForbiddenException("Somente concessionárias e oficinas certificadas registram ordens de serviço.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var performedAt = request.PerformedAt?.UtcDateTime ?? now;
        if (performedAt > now.AddMinutes(5))
            throw new BusinessRuleException("A data de execução do serviço não pode estar no futuro.");
        order.PerformedAt = performedAt;

        var lastMileage = vehicle.ServiceOrders.Count == 0 ? 0 : vehicle.ServiceOrders.Max(o => o.Mileage);
        if (request.Mileage < lastMileage)
            throw new BusinessRuleException(
                $"Quilometragem ({request.Mileage} km) menor que a do último serviço registrado ({lastMileage} km). Possível adulteração de hodômetro.");

        vehicle.CurrentMileage = Math.Max(vehicle.CurrentMileage, request.Mileage);
        db.ServiceOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.ToResponse(vehicle.Vin);
    }

    public async Task<ServiceOrderResponse> GetServiceOrderAsync(string vin, Guid id, CancellationToken ct)
    {
        var vehicle = await FindAsync(vin, tracking: false, ct);
        var order = await db.ServiceOrders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && o.VehicleId == vehicle.Id, ct)
            ?? throw new NotFoundException($"Ordem de serviço {id} não encontrada para o VIN {vehicle.Vin}.");
        return order.ToResponse(vehicle.Vin);
    }

    private async Task<Vehicle> FindAsync(string vin, bool tracking, CancellationToken ct)
    {
        var normalized = Vin.Normalize(vin);
        var q = tracking ? db.Vehicles : db.Vehicles.AsNoTracking();
        return await q.FirstOrDefaultAsync(v => v.Vin == normalized, ct)
               ?? throw new NotFoundException($"Veículo com VIN {normalized} não encontrado.");
    }

    private void EnsureDealerOwns(Vehicle vehicle)
    {
        if (currentUser.IsInRole(Roles.Dealer) && vehicle.HomeDealershipId != currentUser.DealershipId)
            throw new ForbiddenException("Este veículo pertence à carteira de outra concessionária.");
    }
}
