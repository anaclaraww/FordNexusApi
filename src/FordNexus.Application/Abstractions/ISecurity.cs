using FordNexus.Domain.Entities;

namespace FordNexus.Application.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc, int ExpiresInSeconds);

public interface IJwtTokenService
{
    AccessToken Generate(User user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
