using System.Globalization;
using System.Threading.RateLimiting;
using FordNexus.Api.Errors;
using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using Microsoft.Extensions.Options;

namespace FordNexus.Api.Security;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
    public int ApiPermitLimit { get; set; } = 100;
    public int ApiWindowSeconds { get; set; } = 60;
}

public static class RateLimitPolicies
{
    public const string Login = "login";
}

public static class RateLimitingSetup
{
    public static IServiceCollection AddNexusRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitSettings>().Bind(configuration.GetSection(RateLimitSettings.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.Login, http =>
            {
                var settings = Settings(http);
                return RateLimitPartition.GetFixedWindowLimiter($"login:{ClientIp(http)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.LoginPermitLimit,
                    Window = TimeSpan.FromSeconds(settings.LoginWindowSeconds),
                    QueueLimit = 0
                });
            });

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var settings = Settings(http);
                var subject = http.User.FindFirst(NexusClaimTypes.Subject)?.Value;
                var key = subject is not null ? $"user:{subject}" : $"ip:{ClientIp(http)}";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.ApiPermitLimit,
                    Window = TimeSpan.FromSeconds(settings.ApiWindowSeconds),
                    QueueLimit = 0
                });
            });

            options.OnRejected = async (context, _) =>
            {
                var http = context.HttpContext;
                var policy = http.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName ?? "global";
                http.RequestServices.GetRequiredService<INexusMetrics>().RateLimited(policy);
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("FordNexus.Security").LogWarning(
                    SecurityEvents.RateLimited, "{Event} política {Policy} cliente {ClientIp} em {Path}",
                    SecurityEvents.RateLimited.Name, policy, ClientIp(http), http.Request.Path.Value);

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                await ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "Muitas requisições", "Limite de requisições excedido. Aguarde e tente novamente.", "rate_limited");
            };
        });

        return services;
    }

    private static RateLimitSettings Settings(HttpContext http) =>
        http.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

    private static string ClientIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
