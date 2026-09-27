namespace FordNexus.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Vin { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int ModelYear { get; set; }
    public int CurrentMileage { get; set; }

    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;

    public bool ContactConsent { get; set; }

    public bool DataSharingConsent { get; set; }

    public bool TelemetryConsent { get; set; }

    public DateTime? LastTelemetryAt { get; set; }

    public Guid HomeDealershipId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ServiceOrder> ServiceOrders { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
}
