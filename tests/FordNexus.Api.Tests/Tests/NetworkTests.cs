using System.Net;
using System.Net.Http.Json;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Infrastructure.Persistence;

namespace FordNexus.Api.Tests.Tests;

public sealed class NetworkTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    private static string NewCnpj() => string.Concat(Enumerable.Range(0, 14).Select(_ => Random.Shared.Next(10)));

    [Fact]
    public async Task FilaDeManutencao_ComoConcessionaria_RetornaVeiculosVencidosPorUrgencia()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);

        var response = await dealer.GetAsync($"/api/v1/dealerships/{SeedData.DealershipSorocabaId}/maintenance-queue");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var queue = await response.ReadAsAsync<MaintenanceQueueResponse>();
        var vins = queue.Items.Select(i => i.Vin).ToList();

        Assert.Contains(SeedData.KaVin, vins);
        Assert.Contains(SeedData.EcoSportVin, vins);
        Assert.DoesNotContain(SeedData.RangerVin, vins);
        Assert.DoesNotContain(SeedData.FiestaVin, vins);
        Assert.Equal(queue.Items.OrderByDescending(i => i.UrgencyScore).Select(i => i.Vin), vins);
        Assert.All(queue.Items, i => Assert.NotEmpty(i.Reasons));
    }

    [Fact]
    public async Task FilaDeManutencao_DeOutraConcessionaria_Retorna403()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var response = await dealer.GetAsync($"/api/v1/dealerships/{SeedData.DealershipCampinasId}/maintenance-queue");
        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task FilaDeManutencao_ComoOficina_Retorna403()
    {
        var workshop = await factory.CreateClientAsAsync(TestUsers.Workshop);
        var response = await workshop.GetAsync($"/api/v1/dealerships/{SeedData.DealershipSorocabaId}/maintenance-queue");
        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Concessionaria_Inexistente_Retorna404()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var response = await admin.GetAsync($"/api/v1/dealerships/{Guid.NewGuid()}");
        await response.AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Oficina_CadastroECertificacao_FicaVisivelPublicamente()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var create = await admin.PostAsJsonAsync("/api/v1/workshops", new
        {
            name = "Mecânica Votorantim", cnpj = NewCnpj(), city = "Votorantim", state = "SP",
            distributorDealershipId = SeedData.DealershipSorocabaId
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var workshop = await create.ReadAsAsync<WorkshopResponse>();
        Assert.Equal("Pending", workshop.CertificationStatus);

        var anonymous = factory.CreateClient();
        await (await anonymous.GetAsync($"/api/v1/workshops/{workshop.Id}")).AssertProblemAsync(404);

        var certify = await admin.PutAsJsonAsync($"/api/v1/workshops/{workshop.Id}/certification", new { status = "Certified" });
        Assert.Equal(HttpStatusCode.OK, certify.StatusCode);
        Assert.NotNull((await certify.ReadAsAsync<WorkshopResponse>()).CertifiedAt);

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/workshops/{workshop.Id}")).StatusCode);
    }

    [Fact]
    public async Task Oficina_CnpjDuplicado_Retorna409()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var response = await admin.PostAsJsonAsync("/api/v1/workshops", new
        {
            name = "Duplicada", cnpj = "77888999000163", city = "Sorocaba", state = "SP"
        });
        await response.AssertProblemAsync(409, "conflict");
    }

    [Fact]
    public async Task Oficina_StatusInvalido_Retorna400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var response = await admin.PutAsJsonAsync($"/api/v1/workshops/{SeedData.WorkshopPendingId}/certification", new { status = "Aprovadissima" });
        await response.AssertProblemAsync(400);
    }

    [Fact]
    public async Task Oficina_CadastroComoConcessionaria_Retorna403()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var response = await dealer.PostAsJsonAsync("/api/v1/workshops", new
        {
            name = "Não autorizada", cnpj = NewCnpj(), city = "Sorocaba", state = "SP"
        });
        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Usuario_CriarComoAdmin_Retorna201EConsegueLogar()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var email = $"novo.{Guid.NewGuid():N}@fordnexus.com";

        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            name = "Novo Parceiro", email, password = "Senha@Forte1", role = "Partner"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var token = await factory.CreateClient().LoginAsync((email, "Senha@Forte1"));
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Usuario_EmailDuplicado_Retorna409()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            name = "Duplicado", email = TestUsers.Dealer.Email, password = "Senha@Forte1", role = "Admin"
        });
        await response.AssertProblemAsync(409, "conflict");
    }

    [Fact]
    public async Task Usuario_DealerSemConcessionaria_Retorna400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            name = "Dealer sem loja", email = $"x.{Guid.NewGuid():N}@fordnexus.com", password = "Senha@Forte1", role = "Dealer"
        });
        var body = await response.AssertProblemAsync(400, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty("DealershipId", out _));
    }

    [Fact]
    public async Task Usuario_SenhaFraca_Retorna400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            name = "Senha fraca", email = $"y.{Guid.NewGuid():N}@fordnexus.com", password = "12345678", role = "Admin"
        });
        await response.AssertProblemAsync(400);
    }

    [Fact]
    public async Task Usuario_ListarComoConcessionaria_Retorna403()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var response = await dealer.GetAsync("/api/v1/users");
        await response.AssertProblemAsync(403, "forbidden");
    }
}
