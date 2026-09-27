using System.Text.Json.Serialization;
using FordNexus.Api.Auth;
using FordNexus.Api.Errors;
using FordNexus.Api.Observability;
using FordNexus.Api.Security;
using FordNexus.Api.Swagger;
using FordNexus.Application;
using FordNexus.Infrastructure;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
var isProduction = builder.Environment.IsProduction();

builder.AddNexusLogging();
builder.AddNexusHardening();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, allowEphemeralSecrets: !isProduction)
    .AddNexusAuthentication()
    .AddNexusRateLimiting(builder.Configuration)
    .AddNexusSwagger();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ProblemResponses.FromModelState);

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ProblemResponses.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

var metricsPort = builder.Configuration.GetValue<int?>("Metrics:Port");
if (metricsPort is > 0)
    builder.Services.AddMetricServer(o => o.Port = (ushort)metricsPort.Value);

var app = builder.Build();

if (!isProduction && string.IsNullOrWhiteSpace(app.Configuration["Jwt:SigningKey"]))
    app.Logger.LogWarning("Jwt:SigningKey não configurada: usando chave temporária gerada na subida (tokens morrem ao reiniciar).");

await app.Services.SeedDatabaseAsync();

app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (isProduction)
    app.UseHsts();

if (!isProduction)
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Ford Nexus API v1");
        o.DocumentTitle = "Ford Nexus API";
    });
}

app.UseRequestSizeGuard();
app.UseRouting();
app.UseHttpMetrics();
app.UseCors(HardeningSetup.CorsPolicy);
app.UseAuthentication();
app.UseRequestLogScope();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
if (!isProduction)
    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

app.Run();

public partial class Program { }
