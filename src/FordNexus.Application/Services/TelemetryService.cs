using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FordNexus.Application.Services;

public interface ITelemetryService
{
    Task<TelemetryResult> IngestOdometerAsync(string vin, TelemetryReading reading, CancellationToken ct);
}

public sealed class TelemetryService(
    INexusDbContext db,
    TimeProvider time,
    INexusMetrics metrics,
    ILogger<TelemetryService> logger) : ITelemetryService
{
    public const int MaxJumpKm = 5_000;
    public static readonly TimeSpan MaxReadingAge = TimeSpan.FromDays(30);

    public async Task<TelemetryResult> IngestOdometerAsync(string vin, TelemetryReading reading, CancellationToken ct)
    {
        if (!Vin.IsValid(vin))
            return Reject(vin, "VIN inválido no tópico");

        var normalized = Vin.Normalize(vin);
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Vin == normalized, ct);
        if (vehicle is null)
            return Reject(normalized, "veículo não cadastrado");

        if (!vehicle.TelemetryConsent)
            return Reject(normalized, "proprietário sem consentimento de telemetria (LGPD), leitura descartada");

        if (reading.OdometerKm is < 0 or > 2_000_000)
            return Reject(normalized, "hodômetro fora da faixa válida");

        var now = time.GetUtcNow().UtcDateTime;
        var recordedAt = reading.RecordedAt.UtcDateTime;
        if (recordedAt > now.AddMinutes(5))
            return Reject(normalized, "leitura com data no futuro");
        if (recordedAt < now - MaxReadingAge)
            return Reject(normalized, "leitura antiga demais");

        if (reading.OdometerKm < vehicle.CurrentMileage)
        {
            metrics.TelemetryMessage("ignored");
            logger.LogWarning(SecurityEvents.TelemetryRejected,
                "{Event} {Vin}: hodômetro {Odometer} km menor que o registrado {Current} km, possível adulteração",
                SecurityEvents.TelemetryRejected.Name, Mask(normalized), reading.OdometerKm, vehicle.CurrentMileage);
            return new TelemetryResult(TelemetryOutcome.Ignored, "hodômetro menor que o registrado");
        }

        if (reading.OdometerKm - vehicle.CurrentMileage > MaxJumpKm)
            return Reject(normalized, $"salto de {reading.OdometerKm - vehicle.CurrentMileage} km é implausível");

        vehicle.CurrentMileage = reading.OdometerKm;
        vehicle.LastTelemetryAt = now;
        await db.SaveChangesAsync(ct);

        metrics.TelemetryMessage("accepted");
        logger.LogInformation(SecurityEvents.TelemetryAccepted,
            "{Event} {Vin}: hodômetro atualizado para {Odometer} km",
            SecurityEvents.TelemetryAccepted.Name, Mask(normalized), reading.OdometerKm);
        return new TelemetryResult(TelemetryOutcome.Accepted, "quilometragem atualizada");
    }

    private TelemetryResult Reject(string vin, string reason)
    {
        metrics.TelemetryMessage("rejected");
        logger.LogWarning(SecurityEvents.TelemetryRejected, "{Event} {Vin}: {Reason}",
            SecurityEvents.TelemetryRejected.Name, Mask(vin), reason);
        return new TelemetryResult(TelemetryOutcome.Rejected, reason);
    }

    private static string Mask(string vin) => vin.Length > 6 ? "***" + vin[^6..] : "***";
}
