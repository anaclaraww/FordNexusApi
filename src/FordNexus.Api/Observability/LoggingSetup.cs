using FordNexus.Application.Common;

namespace FordNexus.Api.Observability;

public static class LoggingSetup
{
    public static WebApplicationBuilder AddNexusLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
        return builder;
    }

    public static IApplicationBuilder UseRequestLogScope(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("FordNexus.Request");
        var scope = new Dictionary<string, object?>
        {
            ["TraceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            ["UserId"] = context.User.FindFirst(NexusClaimTypes.Subject)?.Value,
            ["Role"] = context.User.FindFirst(NexusClaimTypes.Role)?.Value,
            ["ClientIp"] = context.Connection.RemoteIpAddress?.ToString(),
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.Value
        };

        using (logger.BeginScope(scope))
            await next();
    });
}
