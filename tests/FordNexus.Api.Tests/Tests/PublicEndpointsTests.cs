using System.Net;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Infrastructure.Persistence;

namespace FordNexus.Api.Tests.Tests;

public sealed class PublicEndpointsTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    [Fact]
    public async Task Health_EhPublico_Retorna200()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerJson_EstaDisponivel_ComEsquemaBearer()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.ReadJsonAsync();
        Assert.Equal("Ford Nexus API", doc.GetProperty("info").GetProperty("title").GetString());
        Assert.True(doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        Assert.True(doc.GetProperty("paths").TryGetProperty("/api/v1/vehicles/{vin}/history", out _));
    }

    [Fact]
    public async Task ListarOficinas_Anonimo_RetornaSomenteCertificadas()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/workshops");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.ReadAsAsync<PagedResponse<WorkshopResponse>>();
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, w => Assert.Equal("Certified", w.CertificationStatus));
        Assert.DoesNotContain(page.Items, w => w.Id == SeedData.WorkshopPendingId);
    }

    [Fact]
    public async Task ListarOficinas_ComoAdmin_IncluiPendentes()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Admin);

        var page = await (await client.GetAsync("/api/v1/workshops?status=Pending")).ReadAsAsync<PagedResponse<WorkshopResponse>>();

        Assert.Contains(page.Items, w => w.Id == SeedData.WorkshopPendingId);
    }

    [Fact]
    public async Task ObterOficinaPendente_Anonimo_Retorna404()
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/workshops/{SeedData.WorkshopPendingId}");
        await response.AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Paginacao_ForaDoLimite_Retorna400()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/workshops?pageSize=500");
        await response.AssertProblemAsync(400, "validation_error");
    }

    [Fact]
    public async Task EndpointProtegido_SemToken_Retorna401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/vehicles");
        await response.AssertProblemAsync(401, "unauthenticated");
    }
}
