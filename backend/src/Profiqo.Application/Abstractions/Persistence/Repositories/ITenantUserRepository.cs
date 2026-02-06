using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Users;
using Profiqo.Application.Tenants.Users;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface ITenantUserRepository
{
    Task<IReadOnlyList<TenantUserItemDto>> ListAsync(TenantId tenantId, CancellationToken ct);
    Task<bool> ExistsByTenantEmailAsync(TenantId tenantId, string email, CancellationToken ct);
    Task<User?> GetByTenantAndIdAsync(TenantId tenantId, Guid userId, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}