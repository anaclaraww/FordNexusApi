using FordNexus.Application.Contracts;
using FordNexus.Domain.Entities;

namespace FordNexus.Application.Mappings;

internal static class MappingExtensions
{
    public static UserResponse ToResponse(this User u) =>
        new(u.Id, u.Name, u.Email, u.Role.ToString(), u.DealershipId, u.WorkshopId, u.CreatedAt);

    public static VehicleResponse ToResponse(this Vehicle v) =>
        new(v.Id, v.Vin, v.Model, v.ModelYear, v.CurrentMileage, v.OwnerName, v.OwnerPhone,
            v.ContactConsent, v.DataSharingConsent, v.HomeDealershipId, v.CreatedAt,
            v.TelemetryConsent, v.LastTelemetryAt);

    public static ServiceOrderResponse ToResponse(this ServiceOrder o, string vin) =>
        new(o.Id, vin, o.ServiceType.ToString(), o.Description, o.Mileage, o.Amount, o.GenuineParts,
            o.PerformedAt, o.DealershipId, o.WorkshopId,
            o.WorkshopId is not null ? "Workshop" : "Dealership");

    public static AppointmentResponse ToResponse(this Appointment a, Vehicle vehicle) =>
        new(a.Id, vehicle.Vin, vehicle.Model, a.DealershipId, a.WorkshopId, a.ServiceType.ToString(),
            a.ScheduledAt, a.Status.ToString(), a.Channel, a.Notes, a.CreatedAt, a.UpdatedAt);

    public static DealershipResponse ToResponse(this Dealership d) =>
        new(d.Id, d.Name, d.Cnpj, d.City, d.State, d.ServiceBays);

    public static AuditEntryResponse ToResponse(this AuditEntry a) =>
        new(a.Id, a.Timestamp, a.Action, a.Resource, a.ResourceId, a.UserId, a.Role, a.IpAddress, a.TraceId, a.Details);

    public static WorkshopResponse ToResponse(this Workshop w) =>
        new(w.Id, w.Name, w.Cnpj, w.City, w.State, w.Status.ToString(), w.CertifiedAt, w.DistributorDealershipId);
}
