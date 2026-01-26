using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class ProviderConnectionRepository : IProviderConnectionRepository
{
    private readonly ProfiqoDbContext _db;

    public ProviderConnectionRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public Task<ProviderConnection?> GetByIdAsync(ProviderConnectionId id, CancellationToken cancellationToken)
        => _db.ProviderConnections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ProviderConnection?> GetByProviderAsync(TenantId tenantId, ProviderType providerType, CancellationToken cancellationToken)
        => _db.ProviderConnections.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProviderType == providerType, cancellationToken);

    public async Task AddAsync(ProviderConnection connection, CancellationToken cancellationToken)
    {
        await _db.ProviderConnections.AddAsync(connection, cancellationToken);
    }
}