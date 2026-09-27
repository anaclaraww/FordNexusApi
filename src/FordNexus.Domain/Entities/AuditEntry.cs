namespace FordNexus.Domain.Entities;

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceId { get; set; }
    public string? Details { get; set; }
}
