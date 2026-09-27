using FordNexus.Api.Errors;

namespace FordNexus.Api.Security;

public sealed class RequestLimitSettings
{
    public const string SectionName = "RequestLimits";

    public long MaxBodyBytes { get; set; } = 1_048_576;
}

public static class HardeningSetup
{
    public const string CorsPolicy = "nexus-cors";

    public static WebApplicationBuilder AddNexusHardening(this WebApplicationBuilder builder)
    {
        var limits = builder.Configuration.GetSection(RequestLimitSettings.SectionName).Get<RequestLimitSettings>()
                     ?? new RequestLimitSettings();
        builder.Services.AddOptions<RequestLimitSettings>().Bind(builder.Configuration.GetSection(RequestLimitSettings.SectionName));

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = limits.MaxBodyBytes;
            kestrel.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
        });

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type")));

        builder.Services.AddHsts(o =>
        {
            o.MaxAge = TimeSpan.FromDays(365);
            o.IncludeSubDomains = true;
        });

        return builder;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                headers.CacheControl = "no-store";
            }
            return Task.CompletedTask;
        });

        await next();
    });

    public static IApplicationBuilder UseRequestSizeGuard(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var max = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLimitSettings>>().Value.MaxBodyBytes;
        if (context.Request.ContentLength > max)
        {
            await ProblemResponses.WriteAsync(context, StatusCodes.Status413PayloadTooLarge,
                "Requisição muito grande", $"O corpo da requisição excede o limite de {max} bytes.", "payload_too_large");
            return;
        }
        await next();
    });
}
