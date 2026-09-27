namespace FordNexus.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Role { get; }
    Guid? DealershipId { get; }
    Guid? WorkshopId { get; }
    string? IpAddress { get; }
    string? TraceId { get; }
    bool IsInRole(string role);
}
