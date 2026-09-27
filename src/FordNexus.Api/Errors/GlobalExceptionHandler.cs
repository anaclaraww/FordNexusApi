using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace FordNexus.Api.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, INexusMetrics metrics) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title, code) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Requisição inválida", "validation_error"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Não autenticado", "invalid_credentials"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado", "forbidden"),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado", "not_found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflito com o estado atual do recurso", "conflict"),
            BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", "business_rule_violation"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "internal_error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(SecurityEvents.UnhandledError, exception, "{Event} erro não tratado em {Method} {Path}",
                SecurityEvents.UnhandledError.Name, http.Request.Method, http.Request.Path.Value);
        else if (exception is ForbiddenException)
        {
            metrics.AccessDenied("scope");
            logger.LogWarning(SecurityEvents.ScopeViolation, "{Event} {Method} {Path}: {Message}",
                SecurityEvents.ScopeViolation.Name, http.Request.Method, http.Request.Path.Value, exception.Message);
        }
        else
            logger.LogInformation("{Status} em {Method} {Path}: {Message}", status, http.Request.Method, http.Request.Path, exception.Message);

        var detail = status == StatusCodes.Status500InternalServerError
            ? "Ocorreu um erro inesperado. Informe o traceId ao suporte."
            : exception.Message;

        var problem = ProblemResponses.Create(http, status, title, detail, code);
        if (exception is ValidationException validation)
            problem.Extensions["errors"] = validation.Errors;

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemResponses.ContentType, cancellationToken: ct);
        return true;
    }
}
