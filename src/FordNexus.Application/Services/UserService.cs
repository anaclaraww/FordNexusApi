using FordNexus.Application.Abstractions;
using FordNexus.Application.Common;
using FordNexus.Application.Contracts;
using FordNexus.Application.Mappings;
using FordNexus.Domain.Entities;
using FordNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FordNexus.Application.Services;

public interface IUserService
{
    Task<PagedResponse<UserResponse>> ListAsync(PageQuery query, CancellationToken ct);
    Task<UserResponse> GetAsync(Guid id, CancellationToken ct);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<PermissionReportResponse> GetPermissionReportAsync(CancellationToken ct);
}

public sealed class UserService(INexusDbContext db, IPasswordHasher hasher, TimeProvider time, IAuditLog audit) : IUserService
{
    public async Task<PagedResponse<UserResponse>> ListAsync(PageQuery query, CancellationToken ct)
    {
        var total = await db.Users.CountAsync(ct);
        var items = await db.Users.AsNoTracking()
            .OrderBy(u => u.Email)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);
        return new PagedResponse<UserResponse>(items.Select(u => u.ToResponse()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException($"Usuário {id} não encontrado.");
        return user.ToResponse();
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var role = request.Role!.Value;

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException($"Já existe um usuário com o e-mail {email}.");

        Guid? dealershipId = null, workshopId = null;
        switch (role)
        {
            case UserRole.Dealer:
                if (request.DealershipId is null)
                    throw new ValidationException(nameof(request.DealershipId), "Obrigatório para o perfil Dealer.");
                if (!await db.Dealerships.AnyAsync(d => d.Id == request.DealershipId, ct))
                    throw new BusinessRuleException($"Concessionária {request.DealershipId} não existe.");
                dealershipId = request.DealershipId;
                break;
            case UserRole.Workshop:
                if (request.WorkshopId is null)
                    throw new ValidationException(nameof(request.WorkshopId), "Obrigatório para o perfil Workshop.");
                if (!await db.Workshops.AnyAsync(w => w.Id == request.WorkshopId, ct))
                    throw new BusinessRuleException($"Oficina {request.WorkshopId} não existe.");
                workshopId = request.WorkshopId;
                break;
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            Role = role,
            DealershipId = dealershipId,
            WorkshopId = workshopId,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        db.Users.Add(user);
        audit.Record("user.created", "user", user.Id.ToString(), $"perfil {role}");
        await db.SaveChangesAsync(ct);
        return user.ToResponse();
    }

    public async Task<PermissionReportResponse> GetPermissionReportAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Role).ThenBy(u => u.Email).ToListAsync(ct);
        var dealerships = await db.Dealerships.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var workshops = await db.Workshops.AsNoTracking().ToDictionaryAsync(w => w.Id, ct);

        var entries = users.Select(u =>
        {
            var findings = new List<string>();
            var scope = u.Role switch
            {
                UserRole.Admin => "Global",
                UserRole.Partner => "Histórico por VIN com consentimento",
                UserRole.Dealer when u.DealershipId is { } d && dealerships.TryGetValue(d, out var name) => $"Concessionária {name}",
                UserRole.Workshop when u.WorkshopId is { } w && workshops.TryGetValue(w, out var ws) => $"Oficina {ws.Name} ({ws.Status})",
                _ => "Sem escopo"
            };

            if (scope == "Sem escopo") findings.Add("perfil sem concessionária/oficina válida");
            if (u.Role == UserRole.Admin) findings.Add("privilégio total: confirmar se ainda é necessário");
            if (u.Role == UserRole.Workshop && u.WorkshopId is { } wid && workshops.TryGetValue(wid, out var wk) && !wk.IsCertified)
                findings.Add("oficina sem certificação ativa");
            if (u.LastLoginAt is null && u.CreatedAt < now.AddDays(-30)) findings.Add("nunca acessou (conta criada há mais de 30 dias)");
            if (u.LastLoginAt is { } last && last < now.AddDays(-90)) findings.Add("sem acesso há mais de 90 dias");
            if (u.IsLockedOut(now)) findings.Add("conta bloqueada por tentativas inválidas");

            return new UserPermissionEntry(u.Id, u.Name, u.Email, u.Role.ToString(), scope, u.CreatedAt, u.LastLoginAt,
                u.IsLockedOut(now), findings);
        }).ToList();

        var byRole = entries.GroupBy(e => e.Role).ToDictionary(g => g.Key, g => g.Count());
        return new PermissionReportResponse(now, entries.Count, byRole, entries.Count(e => e.Findings.Count > 0), entries);
    }
}
