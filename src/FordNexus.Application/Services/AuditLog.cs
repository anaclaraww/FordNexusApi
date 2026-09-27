using System.Diagnostics;
using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using FordNexus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FordNexus.Application.Services;

public interface IAuditLog
{
    void Record(string action, string resource, string? resourceId, string? details = null);
    Task<PagedResponse<AuditEntryResponse>> ListAsync(AuditQuery query, CancellationToken ct);
}

public sealed class AuditLog(
    INexusDbContext db,
    ICurrentUser currentUser,
    TimeProvider time,
    INexusMetrics metrics,
    ILogger<AuditLog> logger) : IAuditLog
{
    public void Record(string action, string resource, string? resourceId, string? details = null)
    {
        var entry = new AuditEntry
        {
            Timestamp = time.GetUtcNow().UtcDateTime,
            Action = action,
            Resource = resource,
            ResourceId = resourceId,
            UserId = currentUser.UserId,
            Role = currentUser.Role,
            IpAddress = currentUser.IpAddress,
            TraceId = currentUser.TraceId ?? Activity.Current?.TraceId.ToString(),
            Details = details
        };
        db.AuditEntries.Add(entry);
        metrics.AuditRecorded(action);
        logger.LogInformation(SecurityEvents.AuditRecorded,
            "{Event} {Action} em {Resource} {ResourceId} por {UserId} ({Role}) {Details}",
            SecurityEvents.AuditRecorded.Name, action, resource, resourceId, entry.UserId, entry.Role, details);
    }

    public async Task<PagedResponse<AuditEntryResponse>> ListAsync(AuditQuery query, CancellationToken ct)
    {
        var q = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(a => a.Action == query.Action);
        if (query.UserId is not null) q = q.Where(a => a.UserId == query.UserId);
        if (query.From is not null)
        {
            var from = query.From.Value.UtcDateTime;
            q = q.Where(a => a.Timestamp >= from);
        }
        if (query.To is not null)
        {
            var to = query.To.Value.UtcDateTime;
            q = q.Where(a => a.Timestamp <= to);
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Timestamp)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);
        return new PagedResponse<AuditEntryResponse>(items.Select(a => a.ToResponse()).ToList(), query.Page, query.PageSize, total);
    }
}
