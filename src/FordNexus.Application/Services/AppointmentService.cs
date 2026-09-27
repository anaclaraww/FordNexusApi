using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using FordNexus.Domain.Common;
using FordNexus.Domain.Entities;
using FordNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Application.Services;

public interface IAppointmentService
{
    Task<PagedResponse<AppointmentResponse>> ListAsync(AppointmentQuery query, CancellationToken ct);
    Task<AppointmentResponse> GetAsync(Guid id, CancellationToken ct);
    Task<AppointmentResponse> CreateAsync(CreateAppointmentRequest request, CancellationToken ct);
    Task<AppointmentResponse> UpdateAsync(Guid id, UpdateAppointmentRequest request, CancellationToken ct);
    Task<AppointmentResponse> UpdateStatusAsync(Guid id, UpdateAppointmentStatusRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class AppointmentService(INexusDbContext db, ICurrentUser currentUser, TimeProvider time, IAuditLog audit, INexusMetrics metrics) : IAppointmentService
{
    public async Task<PagedResponse<AppointmentResponse>> ListAsync(AppointmentQuery query, CancellationToken ct)
    {
        var q = ScopedQuery().AsNoTracking();

        if (query.Status is not null) q = q.Where(a => a.Status == query.Status);
        if (query.From is not null)
        {
            var from = query.From.Value.UtcDateTime;
            q = q.Where(a => a.ScheduledAt >= from);
        }
        if (query.To is not null)
        {
            var to = query.To.Value.UtcDateTime;
            q = q.Where(a => a.ScheduledAt <= to);
        }

        var total = await q.CountAsync(ct);
        var items = await q.Include(a => a.Vehicle)
            .OrderBy(a => a.ScheduledAt)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<AppointmentResponse>(
            items.Select(a => a.ToResponse(a.Vehicle!)).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<AppointmentResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, tracking: false, ct);
        return appointment.ToResponse(appointment.Vehicle!);
    }

    public async Task<AppointmentResponse> CreateAsync(CreateAppointmentRequest request, CancellationToken ct)
    {
        var (dealershipId, workshopId) = ResolveLocation(request.DealershipId, request.WorkshopId);

        var vin = Vin.Normalize(request.Vin);
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Vin == vin, ct)
                      ?? throw new BusinessRuleException($"Veículo com VIN {vin} não está cadastrado.");

        await EnsureLocationIsValidAsync(dealershipId, workshopId, ct);

        var scheduledAt = request.ScheduledAt!.Value.UtcDateTime;
        EnsureFuture(scheduledAt);
        await EnsureNoConflictAsync(vehicle.Id, dealershipId, workshopId, scheduledAt, ignoreId: null, ct);

        var now = time.GetUtcNow().UtcDateTime;
        var appointment = new Appointment
        {
            VehicleId = vehicle.Id,
            DealershipId = dealershipId,
            WorkshopId = workshopId,
            ServiceType = request.ServiceType!.Value,
            ScheduledAt = scheduledAt,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        metrics.AppointmentCreated();
        return appointment.ToResponse(vehicle);
    }

    public async Task<AppointmentResponse> UpdateAsync(Guid id, UpdateAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, tracking: true, ct);
        if (!appointment.IsActive)
            throw new ConflictException($"Agendamento com status {appointment.Status} não pode ser remarcado.");

        var scheduledAt = request.ScheduledAt!.Value.UtcDateTime;
        EnsureFuture(scheduledAt);
        await EnsureNoConflictAsync(appointment.VehicleId, appointment.DealershipId, appointment.WorkshopId, scheduledAt, appointment.Id, ct);

        appointment.ScheduledAt = scheduledAt;
        appointment.ServiceType = request.ServiceType!.Value;
        appointment.Notes = request.Notes;
        appointment.Status = AppointmentStatus.Scheduled;
        appointment.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return appointment.ToResponse(appointment.Vehicle!);
    }

    public async Task<AppointmentResponse> UpdateStatusAsync(Guid id, UpdateAppointmentStatusRequest request, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, tracking: true, ct);
        var next = request.Status!.Value;

        if (!appointment.CanTransitionTo(next))
            throw new ConflictException($"Transição de status inválida: {appointment.Status} → {next}.");

        appointment.Status = next;
        appointment.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return appointment.ToResponse(appointment.Vehicle!);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct)
                          ?? throw new NotFoundException($"Agendamento {id} não encontrado.");
        db.Appointments.Remove(appointment);
        audit.Record("appointment.deleted", "appointment", appointment.Id.ToString(), $"status {appointment.Status}");
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Appointment> ScopedQuery()
    {
        IQueryable<Appointment> q = db.Appointments;
        if (currentUser.IsInRole(Roles.Dealer)) q = q.Where(a => a.DealershipId == currentUser.DealershipId);
        else if (currentUser.IsInRole(Roles.Workshop)) q = q.Where(a => a.WorkshopId == currentUser.WorkshopId);
        return q;
    }

    private async Task<Appointment> LoadAsync(Guid id, bool tracking, CancellationToken ct)
    {
        var q = tracking ? db.Appointments : db.Appointments.AsNoTracking();
        var appointment = await q.Include(a => a.Vehicle).FirstOrDefaultAsync(a => a.Id == id, ct)
                          ?? throw new NotFoundException($"Agendamento {id} não encontrado.");

        var inScope =
            currentUser.IsInRole(Roles.Admin) ||
            (currentUser.IsInRole(Roles.Dealer) && appointment.DealershipId == currentUser.DealershipId) ||
            (currentUser.IsInRole(Roles.Workshop) && appointment.WorkshopId == currentUser.WorkshopId);

        if (!inScope)
            throw new ForbiddenException("Este agendamento pertence a outro ponto de atendimento.");
        return appointment;
    }

    private (Guid? DealershipId, Guid? WorkshopId) ResolveLocation(Guid? dealershipId, Guid? workshopId)
    {
        if (currentUser.IsInRole(Roles.Dealer))
        {
            if (workshopId is not null || (dealershipId is not null && dealershipId != currentUser.DealershipId))
                throw new ForbiddenException("Concessionária só pode agendar na própria agenda.");
            return (currentUser.DealershipId, null);
        }

        if (currentUser.IsInRole(Roles.Workshop))
        {
            if (dealershipId is not null || (workshopId is not null && workshopId != currentUser.WorkshopId))
                throw new ForbiddenException("Oficina só pode agendar na própria agenda.");
            return (null, currentUser.WorkshopId);
        }

        if ((dealershipId is null) == (workshopId is null))
            throw new ValidationException("location", "Informe exatamente um local: dealershipId ou workshopId.");
        return (dealershipId, workshopId);
    }

    private async Task EnsureLocationIsValidAsync(Guid? dealershipId, Guid? workshopId, CancellationToken ct)
    {
        if (dealershipId is not null && !await db.Dealerships.AnyAsync(d => d.Id == dealershipId, ct))
            throw new BusinessRuleException($"Concessionária {dealershipId} não existe.");

        if (workshopId is not null)
        {
            var workshop = await db.Workshops.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workshopId, ct)
                           ?? throw new BusinessRuleException($"Oficina {workshopId} não existe.");
            if (!workshop.IsCertified)
                throw new BusinessRuleException("Só é possível agendar em oficinas com certificação Ford ativa.");
        }
    }

    private void EnsureFuture(DateTime scheduledAtUtc)
    {
        if (scheduledAtUtc <= time.GetUtcNow().UtcDateTime)
            throw new BusinessRuleException("O agendamento deve ser para uma data/hora futura.");
    }

    private async Task EnsureNoConflictAsync(Guid vehicleId, Guid? dealershipId, Guid? workshopId,
        DateTime scheduledAt, Guid? ignoreId, CancellationToken ct)
    {
        var active = db.Appointments.Where(a =>
            (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed) &&
            (ignoreId == null || a.Id != ignoreId));

        if (await active.AnyAsync(a => a.VehicleId == vehicleId, ct))
            throw new ConflictException("Este veículo já possui um agendamento ativo.");

        if (await active.AnyAsync(a => a.ScheduledAt == scheduledAt &&
                                       a.DealershipId == dealershipId && a.WorkshopId == workshopId, ct))
            throw new ConflictException("Já existe um agendamento neste horário para este ponto de atendimento.");
    }
}
