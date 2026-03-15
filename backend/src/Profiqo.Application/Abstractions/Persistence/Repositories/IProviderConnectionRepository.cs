using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface IProviderConnectionRepository
{
    Task<ProviderConnection?> GetByIdAsync(ProviderConnectionId id, CancellationToken ct);

    Task<ProviderConnection?> GetByProviderAsync(TenantId tenantId, ProviderType providerType, CancellationToken ct);

    Task AddAsync(ProviderConnection entity, CancellationToken ct);

    Task ClearTrackingAsync(CancellationToken ct);
}