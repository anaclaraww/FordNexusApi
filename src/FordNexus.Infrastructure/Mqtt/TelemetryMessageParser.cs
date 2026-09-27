using System.Text.Json;
using System.Text.RegularExpressions;
using FordNexus.Application.Contracts;

namespace FordNexus.Infrastructure.Mqtt;

public static partial class TelemetryMessageParser
{
    [GeneratedRegex("^vehicles/([A-HJ-NPR-Z0-9]{17})/telemetry$")]
    private static partial Regex TopicRegex();

    public static bool TryParse(string topic, ReadOnlySpan<byte> payload, int maxPayloadBytes,
        out string vin, out TelemetryReading? reading, out string error)
    {
        vin = string.Empty;
        reading = null;

        var match = TopicRegex().Match(topic);
        if (!match.Success)
        {
            error = "tópico fora do padrão vehicles/{VIN}/telemetry";
            return false;
        }
        vin = match.Groups[1].Value;

        if (payload.Length == 0 || payload.Length > maxPayloadBytes)
        {
            error = $"payload vazio ou maior que {maxPayloadBytes} bytes";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("odometerKm", out var odometer)
                || odometer.ValueKind != JsonValueKind.Number || !odometer.TryGetInt32(out var km)
                || !root.TryGetProperty("recordedAt", out var recorded)
                || recorded.ValueKind != JsonValueKind.String || !recorded.TryGetDateTimeOffset(out var at))
            {
                error = "payload precisa de odometerKm (inteiro) e recordedAt (ISO 8601)";
                return false;
            }

            reading = new TelemetryReading(km, at);
            error = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            error = "payload não é um JSON válido";
            return false;
        }
    }
}
