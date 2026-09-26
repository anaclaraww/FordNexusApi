namespace FordNexus.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Role { get; }
    Guid? DealershipId { get; }
    Guid? WorkshopId { get; }
    bool IsInRole(string role);
}
