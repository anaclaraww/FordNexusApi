using FordNexus.Domain.Enums;

namespace FordNexus.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    public Guid? DealershipId { get; set; }

    public Guid? WorkshopId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
