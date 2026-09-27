using FordNexus.Api.Security;
using FordNexus.Application.Contracts;
using FordNexus.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace FordNexus.Api.Controllers;

[SwaggerTag("Autenticação: emissão de token JWT e identificação do usuário logado.")]
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [SwaggerOperation(Summary = "Autentica o usuário e devolve um access token JWT.", Description = "O token expira em 30 minutos (Jwt:ExpirationMinutes). Limite de 5 tentativas por minuto por IP (429) e bloqueio da conta por 15 minutos após 5 senhas erradas.")]
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    [SwaggerOperation(Summary = "Retorna os dados do usuário dono do token.")]
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct) =>
        Ok(await auth.GetCurrentUserAsync(ct));
}
