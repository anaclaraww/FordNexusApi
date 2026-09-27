using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace FordNexus.Infrastructure.Mqtt;

public sealed class MqttTelemetryListener(
    IOptions<MqttOptions> options,
    IServiceScopeFactory scopes,
    INexusMetrics metrics,
    ILogger<MqttTelemetryListener> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var caCertificate = X509Certificate2.CreateFromPem(await File.ReadAllTextAsync(settings.CaCertificatePath, stoppingToken));
        using var pemCertificate = X509Certificate2.CreateFromPemFile(settings.ClientCertificatePath, settings.ClientKeyPath);
        var clientCertificate = new X509Certificate2(pemCertificate.Export(X509ContentType.Pkcs12));

        var client = new MqttFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e => HandleMessageAsync(e, settings, stoppingToken);
        client.DisconnectedAsync += e =>
        {
            metrics.MqttConnectionChanged(false);
            logger.LogWarning(SecurityEvents.MqttConnection, "{Event} desconectado do broker: {Reason}",
                SecurityEvents.MqttConnection.Name, e.Reason);
            return Task.CompletedTask;
        };

        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(settings.Host, settings.Port)
            .WithClientId(settings.ClientId)
            .WithCleanSession()
            .WithTls(new MqttClientOptionsBuilderTlsParameters
            {
                UseTls = true,
                SslProtocol = SslProtocols.Tls12 | SslProtocols.Tls13,
                Certificates = new List<X509Certificate> { clientCertificate },
                CertificateValidationHandler = args => ValidateBrokerCertificate(args.Certificate, args.SslPolicyErrors, caCertificate)
            })
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!client.IsConnected)
            {
                try
                {
                    await client.ConnectAsync(clientOptions, stoppingToken);
                    await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(settings.TopicFilter).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                        .Build(), stoppingToken);

                    metrics.MqttConnectionChanged(true);
                    logger.LogInformation(SecurityEvents.MqttConnection, "{Event} conectado a {Host}:{Port} com mTLS, assinando {Topic}",
                        SecurityEvents.MqttConnection.Name, settings.Host, settings.Port, settings.TopicFilter);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    metrics.MqttConnectionChanged(false);
                    logger.LogError(SecurityEvents.MqttConnection, ex, "{Event} falha ao conectar em {Host}:{Port}",
                        SecurityEvents.MqttConnection.Name, settings.Host, settings.Port);
                }
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        if (client.IsConnected)
            await client.DisconnectAsync();
        client.Dispose();
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e, MqttOptions settings, CancellationToken ct)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.PayloadSegment;

        if (!TelemetryMessageParser.TryParse(topic, payload.AsSpan(), settings.MaxPayloadBytes, out var vin, out var reading, out var error))
        {
            metrics.TelemetryMessage("rejected");
            logger.LogWarning(SecurityEvents.TelemetryRejected, "{Event} mensagem inválida no tópico {Topic}: {Reason}",
                SecurityEvents.TelemetryRejected.Name, topic, error);
            return;
        }

        using var scope = scopes.CreateScope();
        var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        await telemetry.IngestOdometerAsync(vin, reading!, ct);
    }

    // só aceita o broker se o certificado dele foi assinado pela nossa CA e o nome bate com o host
    internal static bool ValidateBrokerCertificate(X509Certificate? certificate, SslPolicyErrors errors, X509Certificate2 trustedCa)
    {
        if (certificate is null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(trustedCa);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(new X509Certificate2(certificate));
    }
}
