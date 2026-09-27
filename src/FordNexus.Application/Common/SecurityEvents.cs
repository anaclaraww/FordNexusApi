using Microsoft.Extensions.Logging;

namespace FordNexus.Application.Common;

public static class SecurityEvents
{
    public static readonly EventId LoginSucceeded = new(1001, "auth.login.success");
    public static readonly EventId LoginFailed = new(1002, "auth.login.failed");
    public static readonly EventId AccountLockedOut = new(1003, "auth.lockout");
    public static readonly EventId TokenRejected = new(1004, "auth.token.rejected");
    public static readonly EventId AccessForbidden = new(1101, "authz.forbidden");
    public static readonly EventId ScopeViolation = new(1102, "authz.scope_denied");
    public static readonly EventId RateLimited = new(1201, "ratelimit.rejected");
    public static readonly EventId AuditRecorded = new(1301, "audit.recorded");
    public static readonly EventId TelemetryAccepted = new(1401, "telemetry.accepted");
    public static readonly EventId TelemetryRejected = new(1402, "telemetry.rejected");
    public static readonly EventId MqttConnection = new(1403, "mqtt.connection");
    public static readonly EventId UnhandledError = new(1501, "app.error");

    public static string EmailFingerprint(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }
}
