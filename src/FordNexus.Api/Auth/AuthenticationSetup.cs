using System.Text;
using FordNexus.Api.Errors;
using FordNexus.Application.Common;
using FordNexus.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace FordNexus.Api.Auth;

public static class AuthenticationSetup
{
    public static IServiceCollection AddNexusAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Seção 'Jwt' ausente na configuração.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = NexusClaimTypes.Name,
                    RoleClaimType = NexusClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                        var detail = expired
                            ? "O token expirou. Faça login novamente em POST /api/v1/auth/login."
                            : context.AuthenticateFailure is not null
                                ? "Token inválido."
                                : "Autenticação necessária. Envie o header 'Authorization: Bearer {token}'.";

                        context.Response.Headers.WWWAuthenticate = expired
                            ? "Bearer error=\"invalid_token\", error_description=\"The token expired\""
                            : "Bearer";
                        await ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                            "Não autenticado", detail, expired ? "token_expired" : "unauthenticated");
                    },
                    OnForbidden = context => ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                        "Acesso negado", "Seu perfil não tem permissão para acessar este recurso.", "forbidden")
                };
            });

        services.AddAuthorizationBuilder()
            .AddNexusPolicies()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddHttpContextAccessor();
        services.AddScoped<FordNexus.Application.Abstractions.ICurrentUser, HttpCurrentUser>();
        return services;
    }
}
