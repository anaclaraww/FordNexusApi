using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace FordNexus.Api.Errors;

public static class ProblemResponses
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(HttpContext http, int status, string title, string? detail, string code)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = http.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        return problem;
    }

    public static Task WriteAsync(HttpContext http, int status, string title, string? detail, string code)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(Create(http, status, title, detail, code), options: null, contentType: ContentType);
    }

    public static void Customize(ProblemDetailsContext context)
    {
        var p = context.ProblemDetails;
        p.Instance ??= context.HttpContext.Request.Path;
        p.Type ??= $"https://httpstatuses.io/{p.Status}";
        p.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        p.Extensions.TryAdd("code", p.Status switch
        {
            400 => "validation_error",
            401 => "unauthenticated",
            403 => "forbidden",
            404 => "not_found",
            405 => "method_not_allowed",
            415 => "unsupported_media_type",
            _ => "error"
        });
    }

    // [Produces("application/json")] nos controllers sobrescreve o content-type de ObjectResult,
    // por isso a validação volta como JsonResult já com application/problem+json.
    public static IActionResult FromModelState(ActionContext context)
    {
        var http = context.HttpContext;
        var problem = http.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(http, context.ModelState, StatusCodes.Status400BadRequest);
        problem.Instance ??= http.Request.Path;
        problem.Type = "https://httpstatuses.io/400";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        problem.Extensions["code"] = "validation_error";

        return new JsonResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentType = ContentType
        };
    }
}
