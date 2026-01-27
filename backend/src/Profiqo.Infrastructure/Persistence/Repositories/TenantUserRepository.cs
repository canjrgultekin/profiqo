using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Tenants.Users;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Users;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class TenantUserRepository : ITenantUserRepository
{
    private readonly ProfiqoDbContext _db;

    public TenantUserRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TenantUserItemDto>> ListAsync(TenantId tenantId, CancellationToken ct)
    {
        var entities = await _db.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        return entities.Select(x => new TenantUserItemDto(
            x.Id.Value,
            x.Email.Value,
            x.DisplayName,
            x.Status.ToString(),
            x.Roles.Select(r => r.ToString()).ToList(),
            x.CreatedAtUtc
        )).ToList();
    }

    public async Task<bool> ExistsByTenantEmailAsync(TenantId tenantId, string email, CancellationToken ct)
    {
        // users table column name is "email"
        var exists = await _db.Users
            .FromSqlInterpolated($@"SELECT * FROM public.users WHERE tenant_id = {tenantId.Value} AND email = {email} LIMIT 1")
            .AsNoTracking()
            .AnyAsync(ct);

        return exists;
    }

    public Task<User?> GetByTenantAndIdAsync(TenantId tenantId, Guid userId, CancellationToken ct)
    {
        return _db.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id.Value == userId, ct);
    }

    public Task AddAsync(User user, CancellationToken ct)
    {
        return _db.Users.AddAsync(user, ct).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}