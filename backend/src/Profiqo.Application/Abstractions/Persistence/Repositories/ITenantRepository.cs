using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Tenants;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken cancellationToken);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);
}