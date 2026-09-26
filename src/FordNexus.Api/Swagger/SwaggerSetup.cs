using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FordNexus.Api.Swagger;

public static class SwaggerSetup
{
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddNexusSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Ford Nexus API",
                Version = "v1",
                Description =
                    "Rede de manutenção autorizada Ford, operada por dados.\n\n" 
            });

            c.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Cole apenas o token JWT (sem o prefixo 'Bearer')."
            });

            c.EnableAnnotations();
            c.OperationFilter<AuthorizeOperationFilter>();
            c.SupportNonNullableReferenceTypes();
        });
        return services;
    }
}

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Description = AppendLine(operation.Description, "🔓 **Público** — não exige token.");
            return;
        }

        var policies = metadata.OfType<IAuthorizeData>()
            .Select(a => a.Policy ?? a.Roles)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        operation.Description = AppendLine(operation.Description,
            policies.Count > 0
                ? $"🔒 **Protegido** — política: `{string.Join("`, `", policies)}`."
                : "🔒 **Protegido** — qualquer usuário autenticado.");

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Token ausente, inválido ou expirado." });
        if (policies.Count > 0)
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Perfil sem permissão para o recurso." });

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SwaggerSetup.BearerScheme }
                }] = Array.Empty<string>()
            }
        };
    }

    private static string AppendLine(string? text, string line) =>
        string.IsNullOrWhiteSpace(text) ? line : $"{text}\n\n{line}";
}
