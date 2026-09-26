using System.Net;
using System.Net.Http.Json;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Infrastructure.Persistence;

namespace FordNexus.Api.Tests.Tests;

public sealed class VehiclesTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    private static object NewVehicle(string vin, bool sharing = true) => new
    {
        vin,
        model = "Territory Titanium",
        modelYear = 2024,
        currentMileage = 15000,
        ownerName = "Cliente de Teste",
        ownerPhone = "+5515990000000",
        contactConsent = true,
        dataSharingConsent = sharing
    };

    [Fact]
    public async Task Criar_ComoConcessionaria_Retorna201ComLocationEVinculaCarteiraDoToken()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var vin = VinGenerator.New();

        var response = await client.PostAsJsonAsync("/api/v1/vehicles", NewVehicle(vin));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/v1/vehicles/{vin}", response.Headers.Location!.ToString());

        var created = await response.ReadAsAsync<VehicleResponse>();
        Assert.Equal(SeedData.DealershipSorocabaId, created.HomeDealershipId);

        var get = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Listar_ComoConcessionaria_RetornaSomenteAPropriaCarteira()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);

        var page = await (await client.GetAsync("/api/v1/vehicles?pageSize=100")).ReadAsAsync<PagedResponse<VehicleResponse>>();

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, v => Assert.Equal(SeedData.DealershipSorocabaId, v.HomeDealershipId));
        Assert.DoesNotContain(page.Items, v => v.Vin == SeedData.KaSedanVin);
    }

    [Fact]
    public async Task Atualizar_ComoConcessionariaDona_Retorna200()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var vin = VinGenerator.New();
        await client.PostAsJsonAsync("/api/v1/vehicles", NewVehicle(vin));

        var response = await client.PutAsJsonAsync($"/api/v1/vehicles/{vin}", new
        {
            ownerName = "Novo Dono",
            ownerPhone = "+5515988887777",
            currentMileage = 16000,
            contactConsent = false,
            dataSharingConsent = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsAsync<VehicleResponse>();
        Assert.Equal("Novo Dono", updated.OwnerName);
        Assert.Equal(16000, updated.CurrentMileage);
    }

    [Fact]
    public async Task Excluir_ComoAdmin_Retorna204EDepois404()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var vin = VinGenerator.New();
        var create = await admin.PostAsJsonAsync("/api/v1/vehicles",
            new { vin, model = "Maverick", modelYear = 2023, currentMileage = 5000, ownerName = "Fulano",
                  ownerPhone = "+5511999990000", homeDealershipId = SeedData.DealershipCampinasId });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var delete = await admin.DeleteAsync($"/api/v1/vehicles/{vin}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await admin.GetAsync($"/api/v1/vehicles/{vin}");
        await get.AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Historico_ComoParceiroComConsentimento_Retorna200SemDadosPessoais()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Partner);

        var response = await client.GetAsync($"/api/v1/vehicles/{SeedData.KaVin}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ownerName", raw);
        Assert.DoesNotContain("ownerPhone", raw);

        var history = await response.ReadAsAsync<VehicleHistoryResponse>();
        Assert.Equal(SeedData.KaVin, history.Vin);
        Assert.Equal(2, history.TotalServices);
        Assert.True(history.Timeline[0].PerformedAt >= history.Timeline[1].PerformedAt, "Timeline deve vir do mais recente para o mais antigo");
    }

    [Fact]
    public async Task RegistrarOrdem_OficinaCertificada_Retorna201EAtualizaQuilometragem()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var vin = VinGenerator.New();
        await dealer.PostAsJsonAsync("/api/v1/vehicles", NewVehicle(vin));

        var workshop = await factory.CreateClientAsAsync(TestUsers.Workshop);
        var response = await workshop.PostAsJsonAsync($"/api/v1/vehicles/{vin}/service-orders", new
        {
            serviceType = "OilChange",
            description = "Troca de óleo Motorcraft",
            mileage = 20000,
            amount = 350.00m,
            genuineParts = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.ReadAsAsync<ServiceOrderResponse>();
        Assert.Equal(SeedData.WorkshopCertifiedId, order.WorkshopId);
        Assert.Equal("Workshop", order.PerformedBy);
        Assert.NotNull(response.Headers.Location);

        var vehicle = await (await dealer.GetAsync($"/api/v1/vehicles/{vin}")).ReadAsAsync<VehicleResponse>();
        Assert.Equal(20000, vehicle.CurrentMileage);
    }

    [Fact]
    public async Task Criar_VinDuplicado_Retorna409()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);

        var response = await client.PostAsJsonAsync("/api/v1/vehicles", NewVehicle(SeedData.KaVin));

        await response.AssertProblemAsync(409, "conflict");
    }

    [Theory]
    [InlineData("CURTO")]
    [InlineData("9BFZH55L0G812345I")]
    [InlineData("9BFZH55L0G8123456789")]
    public async Task Criar_VinInvalido_Retorna400(string vin)
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);

        var response = await client.PostAsJsonAsync("/api/v1/vehicles", NewVehicle(vin));

        var body = await response.AssertProblemAsync(400, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty("Vin", out _));
    }

    [Fact]
    public async Task Obter_VinInexistente_Retorna404()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var response = await client.GetAsync($"/api/v1/vehicles/{VinGenerator.New()}");
        await response.AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Atualizar_QuilometragemMenor_Retorna422()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var vin = VinGenerator.New();
        await client.PostAsJsonAsync("/api/v1/vehicles", NewVehicle(vin));

        var response = await client.PutAsJsonAsync($"/api/v1/vehicles/{vin}", new
        {
            ownerName = "Cliente de Teste", ownerPhone = "+5515990000000", currentMileage = 100,
            contactConsent = true, dataSharingConsent = true
        });

        await response.AssertProblemAsync(422, "business_rule_violation");
    }

    [Fact]
    public async Task RegistrarOrdem_ComQuilometragemMenorQueUltimoServico_Retorna422()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);

        var response = await client.PostAsJsonAsync($"/api/v1/vehicles/{SeedData.KaVin}/service-orders", new
        {
            serviceType = "Revision", description = "Hodômetro suspeito", mileage = 1000, amount = 500m
        });

        await response.AssertProblemAsync(422, "business_rule_violation");
    }

    [Fact]
    public async Task Listar_SemToken_Retorna401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/vehicles");
        await response.AssertProblemAsync(401);
    }

    [Fact]
    public async Task Listar_ComoParceiro_Retorna403()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Partner);
        var response = await client.GetAsync("/api/v1/vehicles");
        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Excluir_ComoConcessionaria_Retorna403()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Dealer);
        var response = await client.DeleteAsync($"/api/v1/vehicles/{SeedData.RangerVin}");
        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Atualizar_VeiculoDeOutraConcessionaria_Retorna403()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.DealerCampinas);

        var response = await client.PutAsJsonAsync($"/api/v1/vehicles/{SeedData.RangerVin}", new
        {
            ownerName = "Invasor", ownerPhone = "+5519990000000", currentMileage = 99999,
            contactConsent = true, dataSharingConsent = true
        });

        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Historico_ComoParceiroSemConsentimento_Retorna403()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Partner);
        var response = await client.GetAsync($"/api/v1/vehicles/{SeedData.FiestaVin}/history");
        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task RegistrarOrdem_OficinaNaoCertificada_Retorna403()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.PendingWorkshop);

        var response = await client.PostAsJsonAsync($"/api/v1/vehicles/{SeedData.RangerVin}/service-orders", new
        {
            serviceType = "Brakes", description = "Pastilhas", mileage = 30000, amount = 400m
        });

        await response.AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task RegistrarOrdem_ComoParceiro_Retorna403()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Partner);

        var response = await client.PostAsJsonAsync($"/api/v1/vehicles/{SeedData.RangerVin}/service-orders", new
        {
            serviceType = "Brakes", description = "Pastilhas", mileage = 30000, amount = 400m
        });

        await response.AssertProblemAsync(403, "forbidden");
    }
}
