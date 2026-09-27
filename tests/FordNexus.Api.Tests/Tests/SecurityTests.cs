using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Domain.Entities;
using FordNexus.Infrastructure.Persistence;
using FordNexus.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FordNexus.Api.Tests.Tests;

public sealed class SecurityTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    [Fact]
    public async Task Lockout_CincoSenhasErradas_BloqueiaInclusiveSenhaCorreta()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var email = $"lock.{Guid.NewGuid():N}@fordnexus.com";
        const string password = "Senha@Forte1";
        (await admin.PostAsJsonAsync("/api/v1/users", new { name = "Conta Teste", email, password, role = "Partner" }))
            .EnsureSuccessStatusCode();

        var anonymous = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var wrong = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada@123" });
            await wrong.AssertProblemAsync(401, "invalid_credentials");
        }

        var correct = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        await correct.AssertProblemAsync(401, "invalid_credentials");
    }

    [Fact]
    public async Task SenhaCorreta_AntesDoLimite_ZeraContadorDeFalhas()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var email = $"reset.{Guid.NewGuid():N}@fordnexus.com";
        const string password = "Senha@Forte1";
        (await admin.PostAsJsonAsync("/api/v1/users", new { name = "Conta Reset", email, password, role = "Partner" }))
            .EnsureSuccessStatusCode();
        var anonymous = factory.CreateClient();

        for (var i = 0; i < 4; i++)
            await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada@123" });
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).StatusCode);

        for (var i = 0; i < 4; i++)
            await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada@123" });
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).StatusCode);
    }

    [Fact]
    public async Task RespostasDaApi_TrazemCabecalhosDeSeguranca()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/workshops");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task CorpoMaiorQueOLimite_Retorna413()
    {
        var huge = new StringContent(new string('a', 2 * 1024 * 1024), Encoding.UTF8, "application/json");

        var response = await factory.CreateClient().PostAsync("/api/v1/auth/login", huge);

        await response.AssertProblemAsync(413, "payload_too_large");
    }

    [Fact]
    public async Task SenhaGiganteNoLogin_Retorna400SemProcessarHash()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Admin.Email, password = new string('x', 5000) });

        var body = await response.AssertProblemAsync(400, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task TokenComAlgoritmoNone_Retorna401()
    {
        static string B64(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var token = $"{B64("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{B64($"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":\"Admin\",\"iss\":\"{jwt.Issuer}\",\"aud\":\"{jwt.Audience}\",\"exp\":{exp}}}")}.";

        var response = await factory.CreateClient().WithBearer(token).GetAsync("/api/v1/users");

        await response.AssertProblemAsync(401);
    }

    [Fact]
    public async Task Cors_OrigemNaoAutorizada_NaoRecebeCabecalhoDeLiberacao()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/vehicles");
        request.Headers.Add("Origin", "https://site-malicioso.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void DadosPessoaisDoDono_SaoGravadosCriptografados()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var converter = db.Model.FindEntityType(typeof(Vehicle))!.FindProperty(nameof(Vehicle.OwnerPhone))!.GetValueConverter();

        Assert.NotNull(converter);
        var stored = (string)converter!.ConvertToProvider("+5515991110001")!;
        Assert.StartsWith(AesGcmFieldEncryptor.Prefix, stored);
        Assert.DoesNotContain("5515991110001", stored);
        Assert.Equal("+5515991110001", converter.ConvertFromProvider(stored));
    }

    [Fact]
    public async Task DadosPessoaisDoDono_ContinuamLegiveisPelaApi()
    {
        var dealer = await factory.CreateClientAsAsync(TestUsers.Dealer);

        var vehicle = await (await dealer.GetAsync($"/api/v1/vehicles/{SeedData.KaVin}")).ReadAsAsync<VehicleResponse>();

        Assert.Equal("Rafael Souza", vehicle.OwnerName);
        Assert.Equal("+5515991110001", vehicle.OwnerPhone);
    }
}

public sealed class FieldEncryptorTests
{
    private static AesGcmFieldEncryptor NewEncryptor(byte[]? key = null) =>
        new(Options.Create(new EncryptionOptions { Key = Convert.ToBase64String(key ?? RandomNumberGenerator.GetBytes(32)) }));

    [Fact]
    public void MesmoTexto_GeraCifrasDiferentes_EVoltaAoOriginal()
    {
        var encryptor = NewEncryptor();

        var a = encryptor.Encrypt("Rafael Souza", "Vehicle.OwnerName");
        var b = encryptor.Encrypt("Rafael Souza", "Vehicle.OwnerName");

        Assert.NotEqual(a, b);
        Assert.Equal("Rafael Souza", encryptor.Decrypt(a, "Vehicle.OwnerName"));
    }

    [Fact]
    public void CifraAdulterada_FalhaNaVerificacaoDeIntegridade()
    {
        var encryptor = NewEncryptor();
        var stored = encryptor.Encrypt("+5515991110001", "Vehicle.OwnerPhone");
        var bytes = Convert.FromBase64String(stored[AesGcmFieldEncryptor.Prefix.Length..]);
        bytes[^1] ^= 0xFF;
        var tampered = AesGcmFieldEncryptor.Prefix + Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => encryptor.Decrypt(tampered, "Vehicle.OwnerPhone"));
    }

    [Fact]
    public void CifraCopiadaParaOutraColuna_NaoDecifra()
    {
        var encryptor = NewEncryptor();
        var phone = encryptor.Encrypt("+5515991110001", "Vehicle.OwnerPhone");

        Assert.ThrowsAny<CryptographicException>(() => encryptor.Decrypt(phone, "Vehicle.OwnerName"));
    }

    [Fact]
    public void ChaveErrada_NaoDecifra()
    {
        var stored = NewEncryptor().Encrypt("Rafael Souza", "Vehicle.OwnerName");

        Assert.ThrowsAny<CryptographicException>(() => NewEncryptor().Decrypt(stored, "Vehicle.OwnerName"));
    }

    [Fact]
    public void ChaveComTamanhoErrado_ERecusada()
    {
        Assert.Throws<InvalidOperationException>(() => NewEncryptor(RandomNumberGenerator.GetBytes(16)));
    }
}

public sealed class RateLimitTests(StrictRateLimitApiFactory factory) : IClassFixture<StrictRateLimitApiFactory>
{
    [Fact]
    public async Task Login_SextaTentativaNoMesmoMinuto_Retorna429ComRetryAfter()
    {
        var client = factory.CreateClient();
        var body = new { email = TestUsers.Partner.Email, password = TestUsers.Partner.Password };

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", body)).StatusCode);

        var blocked = await client.PostAsJsonAsync("/api/v1/auth/login", body);

        await blocked.AssertProblemAsync(429, "rate_limited");
        Assert.True(blocked.Headers.RetryAfter is not null, "Retry-After ausente");
    }
}

public sealed class ProductionTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    [Fact]
    public async Task EmProducao_SwaggerNaoEExposto()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmProducao_ComChavesConfiguradas_LoginFunciona()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Dealer.Email, password = TestUsers.Dealer.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void EmProducao_SemChaves_AplicacaoNaoSobe()
    {
        using var unsafeFactory = new ProductionWithoutSecretsApiFactory();
        Assert.ThrowsAny<Exception>(() => unsafeFactory.CreateClient());
    }
}
