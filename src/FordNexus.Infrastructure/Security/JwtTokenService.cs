using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FordNexus.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider time) : IJwtTokenService
{
    public AccessToken Generate(User user)
    {
        var jwt = options.Value;
        var now = time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.ExpirationMinutes);

        // o JWT é só assinado, qualquer um consegue ler o payload, então nada de dado sensível aqui
        var claims = new List<Claim>
        {
            new(NexusClaimTypes.Subject, user.Id.ToString()),
            new(NexusClaimTypes.Email, user.Email),
            new(NexusClaimTypes.Name, user.Name),
            new(NexusClaimTypes.Role, user.Role.ToString()),
            new(NexusClaimTypes.JwtId, Guid.NewGuid().ToString()),
            new(NexusClaimTypes.IssuedAt, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64)
        };
        if (user.DealershipId is { } dealershipId)
            claims.Add(new Claim(NexusClaimTypes.DealershipId, dealershipId.ToString()));
        if (user.WorkshopId is { } workshopId)
            claims.Add(new Claim(NexusClaimTypes.WorkshopId, workshopId.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires, jwt.ExpirationMinutes * 60);
    }
}
