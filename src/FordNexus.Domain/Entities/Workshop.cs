using FordNexus.Domain.Enums;

namespace FordNexus.Domain.Entities;

public class Workshop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public CertificationStatus Status { get; set; } = CertificationStatus.Pending;
    public DateTime? CertifiedAt { get; set; }

    public Guid? DistributorDealershipId { get; set; }

    public bool IsCertified => Status == CertificationStatus.Certified;

    public void ChangeCertification(CertificationStatus status, DateTime nowUtc)
    {
        Status = status;
        CertifiedAt = status == CertificationStatus.Certified ? nowUtc : CertifiedAt;
    }
}
