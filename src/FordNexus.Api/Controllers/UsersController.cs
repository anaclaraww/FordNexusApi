using FordNexus.Api.Auth;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FordNexus.Api.Controllers;

[SwaggerTag("Gestão de usuários da plataforma (somente Admin).")]
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [SwaggerOperation(Summary = "Lista usuários (paginado).")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(await users.ListAsync(query, ct));

    [SwaggerOperation(Summary = "Relatório de auditoria de permissões.",
        Description = "Lista cada usuário com perfil, escopo, último acesso e achados (Admin a revisar, conta sem uso há 90 dias, oficina sem certificação, conta bloqueada). Base da revisão mensal de acessos.")]
    [HttpGet("permissions-report")]
    [ProducesResponseType(typeof(PermissionReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionReportResponse>> PermissionReport(CancellationToken ct) =>
        Ok(await users.GetPermissionReportAsync(ct));

    [SwaggerOperation(Summary = "Obtém um usuário.")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct) =>
        Ok(await users.GetAsync(id, ct));

    [SwaggerOperation(Summary = "Cria um usuário com um dos perfis: Admin, Dealer, Workshop ou Partner.")]
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }
}
