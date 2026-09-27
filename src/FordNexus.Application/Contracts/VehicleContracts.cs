using System.ComponentModel.DataAnnotations;
using FordNexus.Domain.Enums;

namespace FordNexus.Application.Contracts;

public sealed class VehicleQuery : PageQuery
{
    [StringLength(80)]
    public string? Model { get; set; }
}

public sealed class CreateVehicleRequest
{
    [Required, RegularExpression(FordNexus.Domain.Common.Vin.Pattern, ErrorMessage = "VIN deve ter 17 caracteres alfanuméricos, sem I, O ou Q.")]
    public string Vin { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 2)]
    public string Model { get; set; } = string.Empty;

    [Range(1990, 2030)]
    public int ModelYear { get; set; }

    [Range(0, 2_000_000)]
    public int CurrentMileage { get; set; }

    [Required, StringLength(120, MinimumLength = 3)]
    public string OwnerName { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+?\d{10,15}$", ErrorMessage = "Telefone deve conter de 10 a 15 dígitos.")]
    public string OwnerPhone { get; set; } = string.Empty;

    public bool ContactConsent { get; set; }
    public bool DataSharingConsent { get; set; }
    public bool TelemetryConsent { get; set; }

    public Guid? HomeDealershipId { get; set; }
}

public sealed class UpdateVehicleRequest
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string OwnerName { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+?\d{10,15}$", ErrorMessage = "Telefone deve conter de 10 a 15 dígitos.")]
    public string OwnerPhone { get; set; } = string.Empty;

    [Range(0, 2_000_000)]
    public int CurrentMileage { get; set; }

    public bool ContactConsent { get; set; }
    public bool DataSharingConsent { get; set; }
    public bool TelemetryConsent { get; set; }
}

public sealed record VehicleResponse(
    Guid Id,
    string Vin,
    string Model,
    int ModelYear,
    int CurrentMileage,
    string OwnerName,
    string OwnerPhone,
    bool ContactConsent,
    bool DataSharingConsent,
    Guid HomeDealershipId,
    DateTime CreatedAt,
    bool TelemetryConsent,
    DateTime? LastTelemetryAt);

public sealed class CreateServiceOrderRequest
{
    [Required]
    public ServiceType? ServiceType { get; set; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 2_000_000)]
    public int Mileage { get; set; }

    [Range(typeof(decimal), "0", "1000000")]
    public decimal Amount { get; set; }

    public bool GenuineParts { get; set; } = true;

    public DateTimeOffset? PerformedAt { get; set; }
}

public sealed record ServiceOrderResponse(
    Guid Id,
    string Vin,
    string ServiceType,
    string Description,
    int Mileage,
    decimal Amount,
    bool GenuineParts,
    DateTime PerformedAt,
    Guid? DealershipId,
    Guid? WorkshopId,
    string PerformedBy);

public sealed record VehicleHistoryResponse(
    string Vin,
    string Model,
    int ModelYear,
    int CurrentMileage,
    int TotalServices,
    decimal TotalSpent,
    double GenuinePartsRate,
    DateTime? LastServiceAt,
    IReadOnlyList<ServiceOrderResponse> Timeline);
