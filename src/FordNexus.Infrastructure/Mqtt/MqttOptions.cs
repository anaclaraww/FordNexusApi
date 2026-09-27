namespace FordNexus.Infrastructure.Mqtt;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8883;
    public string ClientId { get; set; } = "ford-nexus-api";
    public string TopicFilter { get; set; } = "vehicles/+/telemetry";
    public string CaCertificatePath { get; set; } = string.Empty;
    public string ClientCertificatePath { get; set; } = string.Empty;
    public string ClientKeyPath { get; set; } = string.Empty;
    public int MaxPayloadBytes { get; set; } = 1024;
}
