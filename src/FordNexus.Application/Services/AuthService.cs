using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    ICurrentUser currentUser,
    TimeProvider time,
    INexusMetrics metrics,
    IAuditLog audit,
    ILogger<AuthService> logger) : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string InvalidCredentials = "E-mail ou senha inválidos.";

    private static string? _dummyHash;

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fingerprint = SecurityEvents.EmailFingerprint(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        var now = time.GetUtcNow().UtcDateTime;

        // mesma resposta pra e-mail inexistente, senha errada e conta bloqueada, assim ninguém descobre quais e-mails existem
        if (user is null)
        {
            _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString());
            hasher.Verify(request.Password, _dummyHash);
            Failed("unknown_user", fingerprint, null);
        }

        if (user.IsLockedOut(now))
            Failed("locked_out", fingerprint, user.Id);

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedLoginAttempts = 0;
                metrics.AccountLockedOut();
                audit.Record("auth.lockout", "user", user.Id.ToString(), $"bloqueado até {user.LockoutEndUtc:O}");
                logger.LogWarning(SecurityEvents.AccountLockedOut,
                    "{Event} usuário {UserId} bloqueado por {Minutes} min após {Max} tentativas inválidas",
                    SecurityEvents.AccountLockedOut.Name, user.Id, LockoutDuration.TotalMinutes, MaxFailedAttempts);
            }
            await db.SaveChangesAsync(ct);
            Failed("wrong_password", fingerprint, user.Id);
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = now;
        await db.SaveChangesAsync(ct);

        metrics.LoginAttempt("success");
        logger.LogInformation(SecurityEvents.LoginSucceeded, "{Event} usuário {UserId} ({Role}) autenticado",
            SecurityEvents.LoginSucceeded.Name, user.Id, user.Role);

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

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Failed(string reason, string emailFingerprint, Guid? userId)
    {
        metrics.LoginAttempt(reason == "locked_out" ? "locked" : "failed");
        logger.LogWarning(SecurityEvents.LoginFailed,
            "{Event} motivo {Reason} conta {EmailFingerprint} usuário {UserId}",
            SecurityEvents.LoginFailed.Name, reason, emailFingerprint, userId);
        throw new AuthenticationFailedException(InvalidCredentials);
    }
}
