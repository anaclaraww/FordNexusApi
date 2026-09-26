using FordNexus.Api.Auth;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FordNexus.Api.Controllers;

[SwaggerTag("Veículos identificados por VIN, ordens de serviço e histórico verificado.")]
[ApiController]
[Route("api/v1/vehicles")]
[Produces("application/json")]
public sealed class VehiclesController(IVehicleService vehicles) : ControllerBase
{
    [SwaggerOperation(Summary = "Lista veículos. Concessionária vê apenas a própria carteira.")]
    [HttpGet]
    [Authorize(Policy = Policies.DealerNetwork)]
    [ProducesResponseType(typeof(PagedResponse<VehicleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<VehicleResponse>>> List([FromQuery] VehicleQuery query, CancellationToken ct) =>
        Ok(await vehicles.ListAsync(query, ct));

    [SwaggerOperation(Summary = "Obtém um veículo pelo VIN.")]
    [HttpGet("{vin}")]
    [Authorize(Policy = Policies.ServiceNetwork)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> GetByVin(string vin, CancellationToken ct) =>
        Ok(await vehicles.GetByVinAsync(vin, ct));

    [SwaggerOperation(Summary = "Cadastra um veículo. Para Dealer, a carteira é sempre a concessionária do token.")]
    [HttpPost]
    [Authorize(Policy = Policies.DealerNetwork)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VehicleResponse>> Create([FromBody] CreateVehicleRequest request, CancellationToken ct)
    {
        var vehicle = await vehicles.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetByVin), new { vin = vehicle.Vin }, vehicle);
    }

    [SwaggerOperation(Summary = "Atualiza dados do proprietário, quilometragem e consentimentos (LGPD).")]
    [HttpPut("{vin}")]
    [Authorize(Policy = Policies.DealerNetwork)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VehicleResponse>> Update(string vin, [FromBody] UpdateVehicleRequest request, CancellationToken ct) =>
        Ok(await vehicles.UpdateAsync(vin, request, ct));

    [SwaggerOperation(Summary = "Remove um veículo e seu histórico (somente Admin — ex.: solicitação de exclusão LGPD).")]
    [HttpDelete("{vin}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string vin, CancellationToken ct)
    {
        await vehicles.DeleteAsync(vin, ct);
        return NoContent();
    }

    [SwaggerOperation(Summary = "Histórico verificado por VIN (V3). Parceiros só acessam com consentimento de compartilhamento.")]
    [HttpGet("{vin}/history")]
    [Authorize(Policy = Policies.HistoryReaders)]
    [ProducesResponseType(typeof(VehicleHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleHistoryResponse>> History(string vin, CancellationToken ct) =>
        Ok(await vehicles.GetHistoryAsync(vin, ct));

    [SwaggerOperation(Summary = "Registra uma ordem de serviço no chassi. O local (concessionária/oficina) vem do token.")]
    [HttpPost("{vin}/service-orders")]
    [Authorize(Policy = Policies.ServiceProviders)]
    [ProducesResponseType(typeof(ServiceOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ServiceOrderResponse>> AddServiceOrder(string vin, [FromBody] CreateServiceOrderRequest request, CancellationToken ct)
    {
        var order = await vehicles.AddServiceOrderAsync(vin, request, ct);
        return CreatedAtAction(nameof(GetServiceOrder), new { vin = order.Vin, id = order.Id }, order);
    }

    [SwaggerOperation(Summary = "Obtém uma ordem de serviço do veículo.")]
    [HttpGet("{vin}/service-orders/{id:guid}")]
    [Authorize(Policy = Policies.ServiceNetwork)]
    [ProducesResponseType(typeof(ServiceOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceOrderResponse>> GetServiceOrder(string vin, Guid id, CancellationToken ct) =>
        Ok(await vehicles.GetServiceOrderAsync(vin, id, ct));
}
