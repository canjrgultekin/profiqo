using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
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

    public Task<ProviderConnection?> GetByIdAsync(ProviderConnectionId id, CancellationToken ct)
        => _db.ProviderConnections.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ProviderConnection?> GetByProviderAsync(TenantId tenantId, ProviderType providerType, CancellationToken ct)
        => _db.ProviderConnections.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProviderType == providerType, ct);

    public async Task AddAsync(ProviderConnection entity, CancellationToken ct)
    {
        await _db.ProviderConnections.AddAsync(entity, ct);
    }

    public Task ClearTrackingAsync(CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        return Task.CompletedTask;
    }
}