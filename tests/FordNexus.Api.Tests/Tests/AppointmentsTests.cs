using System.Net;
using System.Net.Http.Json;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Infrastructure.Persistence;

namespace FordNexus.Api.Tests.Tests;

public sealed class AppointmentsTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    private static DateTimeOffset FutureSlot() =>
        new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(Random.Shared.Next(2, 300)).AddHours(8).AddMinutes(Random.Shared.Next(0, 600));

    private async Task<(HttpClient Dealer, string Vin)> DealerWithNewVehicleAsync()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var vin = VinGenerator.New();
        var response = await dealer.PostAsJsonAsync("/api/v1/vehicles", new
        {
            vin, model = "Ka SE", modelYear = 2017, currentMileage = 60000, ownerName = "Dono do Ka",
            ownerPhone = "+5515990001111", contactConsent = true
        });
        response.EnsureSuccessStatusCode();
        return (dealer, vin);
    }

    private static async Task<AppointmentResponse> CreateAsync(HttpClient client, string vin, DateTimeOffset at)
    {
        var response = await client.PostAsJsonAsync("/api/v1/appointments", new { vin, serviceType = "Revision", scheduledAt = at });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<AppointmentResponse>();
    }

    [Fact]
    public async Task Criar_ComoConcessionaria_Retorna201NaPropriaAgenda()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();

        var response = await dealer.PostAsJsonAsync("/api/v1/appointments", new
        {
            vin, serviceType = "Revision", scheduledAt = FutureSlot(), notes = "Cliente prefere manhã"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsAsync<AppointmentResponse>();
        Assert.Equal("Scheduled", created.Status);
        Assert.Equal("WhatsApp", created.Channel);
        Assert.Equal(SeedData.DealershipSorocabaId, created.DealershipId);
        Assert.EndsWith($"/api/v1/appointments/{created.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task FluxoDeStatus_ScheduledConfirmedCompleted_Retorna200()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        var appointment = await CreateAsync(dealer, vin, FutureSlot());

        var confirm = await dealer.PatchAsJsonAsync($"/api/v1/appointments/{appointment.Id}", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var complete = await dealer.PatchAsJsonAsync($"/api/v1/appointments/{appointment.Id}", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("Completed", (await complete.ReadAsAsync<AppointmentResponse>()).Status);
    }

    [Fact]
    public async Task Remarcar_ComPut_Retorna200EVoltaParaScheduled()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        var appointment = await CreateAsync(dealer, vin, FutureSlot());
        await dealer.PatchAsJsonAsync($"/api/v1/appointments/{appointment.Id}", new { status = "Confirmed" });

        var newSlot = FutureSlot().AddDays(1);
        var response = await dealer.PutAsJsonAsync($"/api/v1/appointments/{appointment.Id}", new
        {
            serviceType = "Brakes", scheduledAt = newSlot
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsAsync<AppointmentResponse>();
        Assert.Equal("Scheduled", updated.Status);
        Assert.Equal("Brakes", updated.ServiceType);
    }

    [Fact]
    public async Task Listar_ComFiltroDeStatus_RetornaSomenteDoEscopo()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        await CreateAsync(dealer, vin, FutureSlot());

        var page = await (await dealer.GetAsync("/api/v1/appointments?status=Scheduled")).ReadAsAsync<PagedResponse<AppointmentResponse>>();

        Assert.Contains(page.Items, a => a.Vin == vin);
        Assert.All(page.Items, a => Assert.Equal(SeedData.DealershipSorocabaId, a.DealershipId));
    }

    [Fact]
    public async Task Excluir_ComoAdmin_Retorna204()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        var appointment = await CreateAsync(dealer, vin, FutureSlot());
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.DeleteAsync($"/api/v1/appointments/{appointment.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await (await admin.GetAsync($"/api/v1/appointments/{appointment.Id}")).AssertProblemAsync(404);
    }

    [Fact]
    public async Task Criar_MesmoVeiculoComAgendamentoAtivo_Retorna409()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        await CreateAsync(dealer, vin, FutureSlot());

        var response = await dealer.PostAsJsonAsync("/api/v1/appointments", new { vin, serviceType = "Tires", scheduledAt = FutureSlot().AddDays(3) });

        await response.AssertProblemAsync(409, "conflict");
    }

    [Fact]
    public async Task Criar_HorarioJaOcupadoNaAgenda_Retorna409()
    {
        var (dealer, vin1) = await DealerWithNewVehicleAsync();
        var (_, vin2) = await DealerWithNewVehicleAsync();
        var slot = FutureSlot();
        await CreateAsync(dealer, vin1, slot);

        var response = await dealer.PostAsJsonAsync("/api/v1/appointments", new { vin = vin2, serviceType = "Revision", scheduledAt = slot });

        await response.AssertProblemAsync(409, "conflict");
    }

    [Fact]
    public async Task Criar_NoPassado_Retorna422()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();

        var response = await dealer.PostAsJsonAsync("/api/v1/appointments", new
        {
            vin, serviceType = "Revision", scheduledAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await response.AssertProblemAsync(422, "business_rule_violation");
    }

    [Fact]
    public async Task Criar_EmOficinaNaoCertificada_Retorna422()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/appointments", new
        {
            vin = SeedData.RangerVin, workshopId = SeedData.WorkshopPendingId, serviceType = "Revision", scheduledAt = FutureSlot()
        });

        await response.AssertProblemAsync(422, "business_rule_violation");
    }

    [Fact]
    public async Task Criar_ComoAdminSemLocal_Retorna400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/appointments", new
        {
            vin = SeedData.RangerVin, serviceType = "Revision", scheduledAt = FutureSlot()
        });

        await response.AssertProblemAsync(400, "validation_error");
    }

    [Fact]
    public async Task Criar_SemCamposObrigatorios_Retorna400()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();

        var response = await dealer.PostAsJsonAsync("/api/v1/appointments", new { vin });

        var body = await response.AssertProblemAsync(400);
        Assert.True(body.GetProperty("errors").TryGetProperty("ServiceType", out _));
        Assert.True(body.GetProperty("errors").TryGetProperty("ScheduledAt", out _));
    }

    [Fact]
    public async Task TransicaoInvalida_CanceladoParaConfirmado_Retorna409()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        var appointment = await CreateAsync(dealer, vin, FutureSlot());
        await dealer.PatchAsJsonAsync($"/api/v1/appointments/{appointment.Id}", new { status = "Cancelled" });

        var response = await dealer.PatchAsJsonAsync($"/api/v1/appointments/{appointment.Id}", new { status = "Confirmed" });

        await response.AssertProblemAsync(409, "conflict");
    }

    [Fact]
    public async Task Obter_IdInexistente_Retorna404()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var response = await dealer.GetAsync($"/api/v1/appointments/{Guid.NewGuid()}");
        await response.AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Obter_AgendamentoDeOutraConcessionaria_Retorna403()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        var appointment = await CreateAsync(dealer, vin, FutureSlot());
        var campinas = await factory.CreateClientAsAsync(TestUsers.DealerCampinas);

        var response = await campinas.GetAsync($"/api/v1/appointments/{appointment.Id}");

        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Criar_ConcessionariaNaAgendaDeOutra_Retorna403()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();

        var response = await dealer.PostAsJsonAsync("/api/v1/appointments", new
        {
            vin, dealershipId = SeedData.DealershipCampinasId, serviceType = "Revision", scheduledAt = FutureSlot()
        });

        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Excluir_ComoConcessionaria_Retorna403()
    {
        var (dealer, vin) = await DealerWithNewVehicleAsync();
        var appointment = await CreateAsync(dealer, vin, FutureSlot());

        var response = await dealer.DeleteAsync($"/api/v1/appointments/{appointment.Id}");

        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Listar_ComoParceiro_Retorna403()
    {
        var partner = await factory.CreateClientAsAsync(TestUsers.Partner);
        var response = await partner.GetAsync("/api/v1/appointments");
        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Listar_SemToken_Retorna401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/appointments");
        await response.AssertProblemAsync(401);
    }
}
