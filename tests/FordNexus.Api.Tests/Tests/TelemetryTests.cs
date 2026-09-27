using System.Text;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using FordNexus.Infrastructure.Mqtt;
using FordNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FordNexus.Api.Tests.Tests;

public sealed class TelemetryTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    private async Task<(TelemetryResult Result, int Mileage)> IngestAsync(string vin, int odometer, DateTimeOffset? at = null)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        var result = await service.IngestOdometerAsync(vin, new TelemetryReading(odometer, at ?? DateTimeOffset.UtcNow), default);
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var mileage = await db.Vehicles.AsNoTracking().Where(v => v.Vin == vin).Select(v => v.CurrentMileage).FirstOrDefaultAsync();
        return (result, mileage);
    }

    [Fact]
    public async Task LeituraValida_ComConsentimento_AtualizaQuilometragem()
    {
        var (result, mileage) = await IngestAsync(SeedData.EcoSportVin, 50_200);

        Assert.Equal(TelemetryOutcome.Accepted, result.Outcome);
        Assert.Equal(50_200, mileage);
    }

    [Fact]
    public async Task VeiculoSemConsentimentoDeTelemetria_LeituraDescartada()
    {
        var (result, mileage) = await IngestAsync(SeedData.RangerVin, 24_000);

        Assert.Equal(TelemetryOutcome.Rejected, result.Outcome);
        Assert.Contains("consentimento", result.Reason);
        Assert.Equal(23_000, mileage);
    }

    [Fact]
    public async Task HodometroMenorQueORegistrado_EIgnorado()
    {
        var (result, mileage) = await IngestAsync(SeedData.KaVin, 10_000);

        Assert.Equal(TelemetryOutcome.Ignored, result.Outcome);
        Assert.True(mileage >= 71_000);
    }

    [Fact]
    public async Task SaltoImplausivel_ERejeitado()
    {
        var (result, _) = await IngestAsync(SeedData.KaVin, 500_000);
        Assert.Equal(TelemetryOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task LeituraComDataNoFuturo_ERejeitada()
    {
        var (result, _) = await IngestAsync(SeedData.KaVin, 71_500, DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(TelemetryOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task VinDesconhecido_ERejeitado()
    {
        var (result, _) = await IngestAsync(VinGenerator.New(), 1_000);
        Assert.Equal(TelemetryOutcome.Rejected, result.Outcome);
    }
}

public sealed class TelemetryMessageParserTests
{
    private static byte[] Json(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void MensagemValida_EInterpretada()
    {
        var ok = TelemetryMessageParser.TryParse($"vehicles/{SeedData.KaVin}/telemetry",
            Json("{\"odometerKm\":72000,\"recordedAt\":\"2026-09-25T10:00:00Z\",\"extra\":1}"), 1024,
            out var vin, out var reading, out _);

        Assert.True(ok);
        Assert.Equal(SeedData.KaVin, vin);
        Assert.Equal(72000, reading!.OdometerKm);
    }

    [Theory]
    [InlineData("vehicles/9BFZH55L0G8123456/comando")]
    [InlineData("vehicles/+/telemetry")]
    [InlineData("vehicles/9BFZH55L0G8123456/telemetry/extra")]
    [InlineData("outro/9BFZH55L0G8123456/telemetry")]
    public void TopicoForaDoPadrao_ERecusado(string topic)
    {
        Assert.False(TelemetryMessageParser.TryParse(topic, Json("{\"odometerKm\":1,\"recordedAt\":\"2026-09-25T10:00:00Z\"}"), 1024,
            out _, out _, out _));
    }

    [Theory]
    [InlineData("nao e json")]
    [InlineData("{\"odometerKm\":\"muito\",\"recordedAt\":\"2026-09-25T10:00:00Z\"}")]
    [InlineData("{\"recordedAt\":\"2026-09-25T10:00:00Z\"}")]
    [InlineData("[1,2,3]")]
    public void PayloadInvalido_ERecusado(string payload)
    {
        Assert.False(TelemetryMessageParser.TryParse($"vehicles/{SeedData.KaVin}/telemetry", Json(payload), 1024,
            out _, out _, out _));
    }

    [Fact]
    public void PayloadMaiorQueOLimite_ERecusado()
    {
        var big = Json("{\"odometerKm\":1,\"recordedAt\":\"2026-09-25T10:00:00Z\",\"pad\":\"" + new string('x', 2000) + "\"}");
        Assert.False(TelemetryMessageParser.TryParse($"vehicles/{SeedData.KaVin}/telemetry", big, 1024, out _, out _, out var error));
        Assert.Contains("1024", error);
    }
}
