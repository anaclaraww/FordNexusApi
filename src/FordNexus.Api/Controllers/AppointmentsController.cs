using FordNexus.Api.Auth;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FordNexus.Api.Controllers;

[SwaggerTag("Agenda de serviços (V1). Cada ponto de atendimento só enxerga a própria agenda.")]
[ApiController]
[Route("api/v1/appointments")]
[Produces("application/json")]
[Authorize(Policy = Policies.ServiceNetwork)]
public sealed class AppointmentsController(IAppointmentService appointments) : ControllerBase
{
    [SwaggerOperation(Summary = "Lista agendamentos do escopo do token, com filtros por status e período.")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<AppointmentResponse>>> List([FromQuery] AppointmentQuery query, CancellationToken ct) =>
        Ok(await appointments.ListAsync(query, ct));

    [SwaggerOperation(Summary = "Obtém um agendamento.")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> GetById(Guid id, CancellationToken ct) =>
        Ok(await appointments.GetAsync(id, ct));

    [SwaggerOperation(Summary = "Cria um agendamento (status inicial: Scheduled).")]
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AppointmentResponse>> Create([FromBody] CreateAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await appointments.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
    }

    [SwaggerOperation(Summary = "Remarca um agendamento (substitui data, tipo de serviço e observações). Volta para Scheduled.")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AppointmentResponse>> Update(Guid id, [FromBody] UpdateAppointmentRequest request, CancellationToken ct) =>
        Ok(await appointments.UpdateAsync(id, request, ct));

    [SwaggerOperation(Summary = "Altera parcialmente o status do agendamento.", Description = "Transições válidas: Scheduled → Confirmed | Cancelled | NoShow; Confirmed → Completed | Cancelled | NoShow.")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> UpdateStatus(Guid id, [FromBody] UpdateAppointmentStatusRequest request, CancellationToken ct) =>
        Ok(await appointments.UpdateStatusAsync(id, request, ct));

    [SwaggerOperation(Summary = "Exclui um agendamento (somente Admin; para desmarcar, use PATCH com status Cancelled).")]
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await appointments.DeleteAsync(id, ct);
        return NoContent();
    }
}
