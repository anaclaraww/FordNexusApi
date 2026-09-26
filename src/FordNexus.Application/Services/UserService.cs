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
}

public sealed class UserService(INexusDbContext db, IPasswordHasher hasher, TimeProvider time) : IUserService
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
        await db.SaveChangesAsync(ct);
        return user.ToResponse();
    }
}
