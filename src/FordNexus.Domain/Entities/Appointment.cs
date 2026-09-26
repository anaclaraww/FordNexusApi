using FordNexus.Domain.Enums;

namespace FordNexus.Domain.Entities;

public class Appointment
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Scheduled] = new[] { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow },
        [AppointmentStatus.Confirmed] = new[] { AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow },
        [AppointmentStatus.Completed] = Array.Empty<AppointmentStatus>(),
        [AppointmentStatus.Cancelled] = Array.Empty<AppointmentStatus>(),
        [AppointmentStatus.NoShow] = Array.Empty<AppointmentStatus>()
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public Guid? DealershipId { get; set; }
    public Guid? WorkshopId { get; set; }

    public ServiceType ServiceType { get; set; }
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public string Channel { get; set; } = "WhatsApp";
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed;

    public bool CanTransitionTo(AppointmentStatus next) => AllowedTransitions[Status].Contains(next);
}
