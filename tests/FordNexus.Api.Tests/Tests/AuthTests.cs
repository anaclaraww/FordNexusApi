using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FordNexus.Api.Tests.Infrastructure;
using FordNexus.Application.Contracts;
using FordNexus.Infrastructure.Persistence;
using FordNexus.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FordNexus.Api.Tests.Tests;

public sealed class AuthTests(NexusApiFactory factory) : IClassFixture<NexusApiFactory>
{
    private JwtOptions Jwt => factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

    [Fact]
    public async Task Login_ComCredenciaisValidas_Retorna200ComTokenEClaimsDoPerfil()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Dealer.Email, password = TestUsers.Dealer.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsAsync<TokenResponse>();
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(Jwt.ExpirationMinutes * 60, body.ExpiresIn);
        Assert.Equal("Dealer", body.User.Role);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal(Jwt.Issuer, jwt.Issuer);
        Assert.Contains(Jwt.Audience, jwt.Audiences);
        Assert.Equal("Dealer", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal(SeedData.DealershipSorocabaId.ToString(), jwt.Claims.Single(c => c.Type == "dealership_id").Value);
        Assert.Equal(body.User.Id.ToString(), jwt.Subject);
        Assert.False(string.IsNullOrEmpty(jwt.Id), "jti ausente");
        Assert.DoesNotContain(jwt.Claims, c => c.Value.Contains(TestUsers.Dealer.Password));

        var expected = DateTime.UtcNow.AddMinutes(Jwt.ExpirationMinutes);
        Assert.InRange(jwt.ValidTo, expected.AddMinutes(-1), expected.AddMinutes(1));
    }

    [Fact]
    public async Task Login_ComSenhaIncorreta_Retorna401Padronizado()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Admin.Email, password = "SenhaErrada1" });

        await response.AssertProblemAsync(401, "invalid_credentials");
    }

    [Fact]
    public async Task Login_ComEmailInexistente_Retorna401ComMesmaMensagem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "ninguem@fordnexus.com", password = "Qualquer1" });

        var body = await response.AssertProblemAsync(401, "invalid_credentials");
        Assert.Equal("E-mail ou senha inválidos.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_ComPayloadInvalido_Retorna400ComErrosPorCampo()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "nao-e-email", password = "" });

        var body = await response.AssertProblemAsync(400);
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Me_SemToken_Retorna401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/auth/me");

        await response.AssertProblemAsync(401, "unauthenticated");
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Me_ComTokenValido_RetornaUsuarioDoToken()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Workshop);

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.ReadAsAsync<UserResponse>();
        Assert.Equal(TestUsers.Workshop.Email, me.Email);
        Assert.Equal("Workshop", me.Role);
        Assert.Equal(SeedData.WorkshopCertifiedId, me.WorkshopId);
    }

    [Fact]
    public async Task TokenExpirado_Retorna401ComCodigoTokenExpired()
    {
        var token = CreateToken(Jwt.SigningKey, Jwt.Issuer, expires: DateTime.UtcNow.AddMinutes(-5));
        var client = factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/auth/me");

        await response.AssertProblemAsync(401, "token_expired");
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task TokenComAssinaturaDeOutraChave_Retorna401()
    {
        var token = CreateToken("uma-chave-completamente-diferente-com-32+-caracteres", Jwt.Issuer, DateTime.UtcNow.AddMinutes(30));
        var response = await factory.CreateClient().WithBearer(token).GetAsync("/api/v1/auth/me");

        await response.AssertProblemAsync(401, "unauthenticated");
    }

    [Fact]
    public async Task TokenComIssuerInvalido_Retorna401()
    {
        var token = CreateToken(Jwt.SigningKey, "emissor-falso", DateTime.UtcNow.AddMinutes(30));
        var response = await factory.CreateClient().WithBearer(token).GetAsync("/api/v1/auth/me");

        await response.AssertProblemAsync(401);
    }

    [Fact]
    public async Task TokenMalformado_Retorna401()
    {
        var response = await factory.CreateClient().WithBearer("isto.nao.e-um-jwt").GetAsync("/api/v1/auth/me");

        await response.AssertProblemAsync(401);
    }

    private string CreateToken(string key, string issuer, DateTime expires)
    {
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("role", "Admin"),
            new Claim("name", "Token de teste")
        };
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: Jwt.Audience,
            claims: claims,
            notBefore: expires.AddHours(-1),
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
