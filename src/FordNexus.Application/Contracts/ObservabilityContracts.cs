using System.ComponentModel.DataAnnotations;

namespace FordNexus.Application.Contracts;

public sealed class AuditQuery : PageQuery
{
    [StringLength(80)]
    public string? Action { get; set; }

    public Guid? UserId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed record AuditEntryResponse(
    Guid Id,
    DateTime Timestamp,
    string Action,
    string Resource,
    string? ResourceId,
    Guid? UserId,
    string? Role,
    string? IpAddress,
    string? TraceId,
    string? Details);

public sealed record UserPermissionEntry(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    string Scope,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool LockedOut,
    IReadOnlyList<string> Findings);

public sealed record PermissionReportResponse(
    DateTime GeneratedAt,
    int TotalUsers,
    IReadOnlyDictionary<string, int> UsersByRole,
    int UsersWithFindings,
    IReadOnlyList<UserPermissionEntry> Users);

public sealed record TelemetryReading(int OdometerKm, DateTimeOffset RecordedAt);

public enum TelemetryOutcome
{
    Accepted,
    Ignored,
    Rejected
}

public sealed record TelemetryResult(TelemetryOutcome Outcome, string Reason);
