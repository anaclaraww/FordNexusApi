using FordNexus.Domain.Enums;

namespace FordNexus.Domain.Entities;

public class ServiceOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public Guid? DealershipId { get; set; }
    public Guid? WorkshopId { get; set; }

    public ServiceType ServiceType { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public decimal Amount { get; set; }
    public bool GenuineParts { get; set; } = true;
    public DateTime PerformedAt { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
