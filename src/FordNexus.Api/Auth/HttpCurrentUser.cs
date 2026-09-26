using System.Security.Claims;
using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;

namespace FordNexus.Api.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public Guid? UserId => ReadGuid(NexusClaimTypes.Subject);
    public string? Role => Principal?.FindFirstValue(NexusClaimTypes.Role);
    public Guid? DealershipId => ReadGuid(NexusClaimTypes.DealershipId);
    public Guid? WorkshopId => ReadGuid(NexusClaimTypes.WorkshopId);

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var id) ? id : null;
}
