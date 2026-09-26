using System.Text.Json.Serialization;
using FordNexus.Api.Auth;
using FordNexus.Api.Errors;
using FordNexus.Api.Swagger;
using FordNexus.Application;
using FordNexus.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddNexusAuthentication(builder.Configuration)
    .AddNexusSwagger();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ProblemResponses.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.Services.SeedDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Ford Nexus API v1");
    o.DocumentTitle = "Ford Nexus API";
    o.EnablePersistAuthorization();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

app.Run();

public partial class Program { }
