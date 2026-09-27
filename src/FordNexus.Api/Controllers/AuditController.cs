using FordNexus.Api.Auth;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FordNexus.Api.Controllers;

[SwaggerTag("Trilha de auditoria de alterações críticas e acessos a dados pessoais.")]
[ApiController]
[Route("api/v1/audit-events")]
[Produces("application/json")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AuditController(IAuditLog audit) : ControllerBase
{
    [SwaggerOperation(Summary = "Lista eventos de auditoria, do mais recente para o mais antigo.",
        Description = "Ações registradas: user.created, auth.lockout, vehicle.deleted, vehicle.consent_changed, vehicle.history_shared, workshop.created, workshop.certification_changed, appointment.deleted.")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AuditEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<AuditEntryResponse>>> List([FromQuery] AuditQuery query, CancellationToken ct) =>
        Ok(await audit.ListAsync(query, ct));
}
