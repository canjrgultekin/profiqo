using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Abstractions.Persistence.Repositories;

public interface IProviderConnectionRepository
{
    Task<ProviderConnection?> GetByIdAsync(ProviderConnectionId id, CancellationToken cancellationToken);

    Task<ProviderConnection?> GetByProviderAsync(
        TenantId tenantId,
        ProviderType providerType,
        CancellationToken cancellationToken);

    Task AddAsync(ProviderConnection connection, CancellationToken cancellationToken);
}