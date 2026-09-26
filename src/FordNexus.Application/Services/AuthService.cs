using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Application.Services;

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserResponse> GetCurrentUserAsync(CancellationToken ct);
}

public sealed class AuthService(
    INexusDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService tokens,
    ICurrentUser currentUser) : IAuthService
{
    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        // mesma resposta pra e-mail inexistente e senha errada, assim ninguém descobre quais e-mails estão cadastrados
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationFailedException("E-mail ou senha inválidos.");

        var token = tokens.Generate(user);
        return new TokenResponse(token.Token, "Bearer", token.ExpiresInSeconds, token.ExpiresAtUtc, user.ToResponse());
    }

    public async Task<UserResponse> GetCurrentUserAsync(CancellationToken ct)
    {
        var id = currentUser.UserId ?? throw new AuthenticationFailedException("Token sem identificação de usuário.");
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("Usuário do token não existe mais.");
        return user.ToResponse();
    }
}
