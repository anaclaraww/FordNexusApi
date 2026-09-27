using System.Net;
using System.Net.Http.Json;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Infrastructure.Persistence;
using Prometheus;

namespace FordNexus.Api.Tests.Tests;

public sealed class ObservabilityTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    [Fact]
    public async Task ExclusaoDeVeiculo_GeraEventoDeAuditoriaComAutorEIp()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var vin = VinGenerator.New();
        (await admin.PostAsJsonAsync("/api/v1/vehicles", new
        {
            vin, model = "Bronco Sport", modelYear = 2024, currentMileage = 1000, ownerName = "Auditoria",
            ownerPhone = "+5511999990000", homeDealershipId = SeedData.DealershipSorocabaId
        })).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/v1/vehicles/{vin}")).EnsureSuccessStatusCode();

        var page = await (await admin.GetAsync("/api/v1/audit-events?action=vehicle.deleted&pageSize=100"))
            .ReadAsAsync<PagedResponse<AuditEntryResponse>>();

        var entry = Assert.Single(page.Items, e => e.ResourceId == vin);
        Assert.Equal("Admin", entry.Role);
        Assert.NotNull(entry.UserId);
        Assert.False(string.IsNullOrEmpty(entry.TraceId));
    }

    [Fact]
    public async Task MudancaDeCertificacao_GeraEventoDeAuditoria()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        (await admin.PutAsJsonAsync($"/api/v1/workshops/{SeedData.WorkshopPendingId}/certification", new { status = "Suspended" }))
            .EnsureSuccessStatusCode();

        var page = await (await admin.GetAsync("/api/v1/audit-events?action=workshop.certification_changed"))
            .ReadAsAsync<PagedResponse<AuditEntryResponse>>();

        Assert.Contains(page.Items, e => e.ResourceId == SeedData.WorkshopPendingId.ToString() && e.Details == "Pending->Suspended");
    }

    [Fact]
    public async Task ConsultaDeHistoricoPorParceiro_FicaRegistrada()
    {
        var partner = await factory.CreateClientAsAsync(TestUsers.Partner);
        (await partner.GetAsync($"/api/v1/vehicles/{SeedData.KaSedanVin}/history")).EnsureSuccessStatusCode();

        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var page = await (await admin.GetAsync("/api/v1/audit-events?action=vehicle.history_shared"))
            .ReadAsAsync<PagedResponse<AuditEntryResponse>>();

        Assert.Contains(page.Items, e => e.ResourceId == SeedData.KaSedanVin && e.Role == "Partner");
    }

    [Fact]
    public async Task TrilhaDeAuditoria_SoAdminAcessa()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        await (await dealer.GetAsync("/api/v1/audit-events")).AssertProblemAsync(403, "forbidden");
        await (await factory.CreateClient().GetAsync("/api/v1/audit-events")).AssertProblemAsync(401);
    }

    [Fact]
    public async Task RelatorioDePermissoes_ApontaAchados()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.GetAsync("/api/v1/users/permissions-report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.ReadAsAsync<PermissionReportResponse>();
        Assert.True(report.TotalUsers >= 6);
        var pending = Assert.Single(report.Users, u => u.Email == TestUsers.PendingWorkshop.Email);
        Assert.Contains(pending.Findings, f => f.Contains("certificação"));
        Assert.Contains(report.Users, u => u.Role == "Admin" && u.Findings.Count > 0);
    }

    [Fact]
    public async Task RelatorioDePermissoes_ConcessionariaRecebe403()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        await (await dealer.GetAsync("/api/v1/users/permissions-report")).AssertProblemAsync(403);
    }

    [Fact]
    public async Task LoginsComFalha_SaoContabilizadosNasMetricas()
    {
        await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "nao.existe@fordnexus.com", password = "Qualquer1" });

        using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        var text = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("fordnexus_auth_login_total{result=\"failed\"}", text);
        Assert.Contains("http_requests_received_total", text);
    }

    [Fact]
    public async Task AcessoNegadoPorEscopo_EContabilizado()
    {
        var campinas = await factory.CreateClientAsAsync(TestUsers.DealerCampinas);
        await campinas.GetAsync($"/api/v1/dealerships/{SeedData.DealershipSorocabaId}/maintenance-queue");

        using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        var text = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("fordnexus_authz_denied_total{reason=\"scope\"}", text);
    }
}
