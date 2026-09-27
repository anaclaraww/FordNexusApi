using System.ComponentModel.DataAnnotations;
using FordNexus.Domain.Enums;

namespace FordNexus.Application.Contracts;

public sealed record DealershipResponse(Guid Id, string Name, string Cnpj, string City, string State, int ServiceBays);

public sealed class WorkshopQuery : PageQuery
{
    [StringLength(80)]
    public string? City { get; set; }

    public CertificationStatus? Status { get; set; }
}

public sealed class CreateWorkshopRequest
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{14}$", ErrorMessage = "CNPJ deve conter 14 dígitos numéricos.")]
    public string Cnpj { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Z]{2}$", ErrorMessage = "UF deve ter 2 letras maiúsculas.")]
    public string State { get; set; } = string.Empty;

    public Guid? DistributorDealershipId { get; set; }
}

public sealed class UpdateCertificationRequest
{
    [Required]
    public CertificationStatus? Status { get; set; }
}

public sealed record WorkshopResponse(
    Guid Id,
    string Name,
    string Cnpj,
    string City,
    string State,
    string CertificationStatus,
    DateTime? CertifiedAt,
    Guid? DistributorDealershipId);

public sealed record MaintenanceQueueItem(
    string Vin,
    string Model,
    int ModelYear,
    string OwnerName,
    string OwnerPhone,
    DateTime? LastServiceAt,
    int KmSinceLastService,
    double? MonthsSinceLastService,
    int UrgencyScore,
    string SuggestedService,
    IReadOnlyList<string> Reasons);

public sealed record MaintenanceQueueResponse(
    Guid DealershipId,
    DateTime GeneratedAt,
    int TotalDue,
    IReadOnlyList<MaintenanceQueueItem> Items);
