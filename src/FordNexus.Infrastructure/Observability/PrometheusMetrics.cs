using FordNexus.Application.Abstractions;
using Prometheus;

namespace FordNexus.Infrastructure.Observability;

public sealed class PrometheusMetrics : INexusMetrics
{
    private static readonly Counter Logins = Metrics.CreateCounter(
        "fordnexus_auth_login_total", "Tentativas de login por resultado.", "result");

    private static readonly Counter Lockouts = Metrics.CreateCounter(
        "fordnexus_auth_lockouts_total", "Contas bloqueadas por excesso de senhas erradas.");

    private static readonly Counter Denied = Metrics.CreateCounter(
        "fordnexus_authz_denied_total", "Acessos negados (403) por motivo.", "reason");

    private static readonly Counter RateLimit = Metrics.CreateCounter(
        "fordnexus_ratelimit_rejected_total", "Requisições recusadas por rate limit.", "policy");

    private static readonly Counter Audit = Metrics.CreateCounter(
        "fordnexus_audit_events_total", "Eventos gravados na trilha de auditoria.", "action");

    private static readonly Counter Appointments = Metrics.CreateCounter(
        "fordnexus_appointments_created_total", "Agendamentos criados.");

    private static readonly Gauge QueueSize = Metrics.CreateGauge(
        "fordnexus_maintenance_queue_size", "Veículos na fila de manutenção na última consulta.", "dealership");

    private static readonly Counter Telemetry = Metrics.CreateCounter(
        "fordnexus_mqtt_messages_total", "Mensagens de telemetria recebidas por resultado.", "result");

    private static readonly Gauge MqttConnected = Metrics.CreateGauge(
        "fordnexus_mqtt_connected", "1 quando a API está conectada ao broker MQTT.");

    public void LoginAttempt(string result) => Logins.WithLabels(result).Inc();
    public void AccountLockedOut() => Lockouts.Inc();
    public void AccessDenied(string reason) => Denied.WithLabels(reason).Inc();
    public void RateLimited(string policy) => RateLimit.WithLabels(policy).Inc();
    public void AuditRecorded(string action) => Audit.WithLabels(action).Inc();
    public void AppointmentCreated() => Appointments.Inc();
    public void MaintenanceQueueCalculated(Guid dealershipId, int size) => QueueSize.WithLabels(dealershipId.ToString()).Set(size);
    public void TelemetryMessage(string result) => Telemetry.WithLabels(result).Inc();
    public void MqttConnectionChanged(bool connected) => MqttConnected.Set(connected ? 1 : 0);
}
