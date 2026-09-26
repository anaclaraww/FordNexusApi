using System.ComponentModel.DataAnnotations;
using FordNexus.Api.Auth;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FordNexus.Api.Controllers;

[SwaggerTag("Concessionárias e a fila de manutenção do dia (Motor 1).")]
[ApiController]
[Route("api/v1/dealerships")]
[Produces("application/json")]
[Authorize(Policy = Policies.DealerNetwork)]
public sealed class DealershipsController(IDealershipService dealerships) : ControllerBase
{
    [SwaggerOperation(Summary = "Lista concessionárias.")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DealershipResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DealershipResponse>>> List(CancellationToken ct) =>
        Ok(await dealerships.ListAsync(ct));

    [SwaggerOperation(Summary = "Obtém uma concessionária.")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DealershipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealershipResponse>> GetById(Guid id, CancellationToken ct) =>
        Ok(await dealerships.GetAsync(id, ct));

    [SwaggerOperation(Summary = "Fila do dia: clientes da carteira que estão na hora da manutenção, por ordem de urgência.", Description = "Regra (manual do proprietário): revisão a cada 10.000 km ou 12 meses; entra na fila a partir de 90% do intervalo. Só considera veículos com consentimento de contato (LGPD) e sem agendamento ativo.")]
    [HttpGet("{id:guid}/maintenance-queue")]
    [ProducesResponseType(typeof(MaintenanceQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceQueueResponse>> MaintenanceQueue(
        Guid id, [FromQuery, Range(1, 200)] int limit = 50, CancellationToken ct = default) =>
        Ok(await dealerships.GetMaintenanceQueueAsync(id, limit, ct));
}
