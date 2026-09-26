using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FordNexus.Application.Contracts;

namespace FordNexus.Api.Tests.Infrastructure;

public static class TestUsers
{
    public static readonly (string Email, string Password) Admin = ("admin@fordnexus.com", "Admin@123");
    public static readonly (string Email, string Password) Dealer = ("concessionaria@fordnexus.com", "Dealer@123");
    public static readonly (string Email, string Password) DealerCampinas = ("campinas@fordnexus.com", "Dealer@123");
    public static readonly (string Email, string Password) Workshop = ("oficina@fordnexus.com", "Oficina@123");
    public static readonly (string Email, string Password) PendingWorkshop = ("oficina.pendente@fordnexus.com", "Oficina@123");
    public static readonly (string Email, string Password) Partner = ("seguradora@fordnexus.com", "Parceiro@123");
}

public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class HttpExtensions
{
    public static async Task<string> LoginAsync(this HttpClient client, (string Email, string Password) user)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = user.Email, password = user.Password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestJson.Options);
        return token!.AccessToken;
    }

    public static async Task<HttpClient> CreateClientAsAsync(this NexusApiFactory factory, (string Email, string Password) user)
    {
        var client = factory.CreateClient();
        var token = await client.LoginAsync(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);

    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, int expectedStatus, string? expectedCode = null)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.ReadJsonAsync();
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.True(body.TryGetProperty("traceId", out _), "ProblemDetails sem traceId");
        if (expectedCode is not null)
            Assert.Equal(expectedCode, body.GetProperty("code").GetString());
        return body;
    }
}

public static class VinGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";

    public static string New() =>
        new(Enumerable.Range(0, 17).Select(_ => Chars[Random.Shared.Next(Chars.Length)]).ToArray());
}
