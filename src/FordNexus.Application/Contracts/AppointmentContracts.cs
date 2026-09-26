using System.ComponentModel.DataAnnotations;
using FordNexus.Domain.Enums;

namespace FordNexus.Application.Contracts;

public sealed class AppointmentQuery : PageQuery
{
    public AppointmentStatus? Status { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed class CreateAppointmentRequest
{
    [Required, RegularExpression(FordNexus.Domain.Common.Vin.Pattern, ErrorMessage = "VIN deve ter 17 caracteres alfanuméricos, sem I, O ou Q.")]
    public string Vin { get; set; } = string.Empty;

    public Guid? DealershipId { get; set; }

    public Guid? WorkshopId { get; set; }

    [Required]
    public ServiceType? ServiceType { get; set; }

    [Required]
    public DateTimeOffset? ScheduledAt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class UpdateAppointmentRequest
{
    [Required]
    public ServiceType? ServiceType { get; set; }

    [Required]
    public DateTimeOffset? ScheduledAt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class UpdateAppointmentStatusRequest
{
    [Required]
    public AppointmentStatus? Status { get; set; }
}

public sealed record AppointmentResponse(
    Guid Id,
    string Vin,
    string VehicleModel,
    Guid? DealershipId,
    Guid? WorkshopId,
    string ServiceType,
    DateTime ScheduledAt,
    string Status,
    string Channel,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
