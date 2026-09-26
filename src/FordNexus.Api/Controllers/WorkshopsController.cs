using FordNexus.Api.Auth;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FordNexus.Api.Controllers;

[SwaggerTag("Rede de oficinas Ford Service Partner (V2).")]
[ApiController]
[Route("api/v1/workshops")]
[Produces("application/json")]
public sealed class WorkshopsController(IWorkshopService workshops) : ControllerBase
{
    [SwaggerOperation(Summary = "Lista oficinas. Público (dono do carro procurando oficina): somente certificadas. Admin: todas.")]
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponse<WorkshopResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<WorkshopResponse>>> List([FromQuery] WorkshopQuery query, CancellationToken ct) =>
        Ok(await workshops.ListAsync(query, ct));

    [SwaggerOperation(Summary = "Obtém uma oficina. Oficinas não certificadas só são visíveis ao Admin.")]
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WorkshopResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkshopResponse>> GetById(Guid id, CancellationToken ct) =>
        Ok(await workshops.GetAsync(id, ct));

    [SwaggerOperation(Summary = "Cadastra uma oficina (status inicial: Pending).")]
    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(WorkshopResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkshopResponse>> Create([FromBody] CreateWorkshopRequest request, CancellationToken ct)
    {
        var workshop = await workshops.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = workshop.Id }, workshop);
    }

    [SwaggerOperation(Summary = "Define a certificação da oficina (Pending, Certified ou Suspended).")]
    [HttpPut("{id:guid}/certification")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(WorkshopResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkshopResponse>> UpdateCertification(Guid id, [FromBody] UpdateCertificationRequest request, CancellationToken ct) =>
        Ok(await workshops.UpdateCertificationAsync(id, request, ct));
}
