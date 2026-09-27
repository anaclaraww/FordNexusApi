namespace FordNexus.Application.Abstractions;

public interface INexusMetrics
{
    void LoginAttempt(string result);
    void AccountLockedOut();
    void AccessDenied(string reason);
    void RateLimited(string policy);
    void AuditRecorded(string action);
    void AppointmentCreated();
    void MaintenanceQueueCalculated(Guid dealershipId, int size);
    void TelemetryMessage(string result);
    void MqttConnectionChanged(bool connected);
}
