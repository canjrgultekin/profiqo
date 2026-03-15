using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Tenants;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class TenantRepository : ITenantRepository
{
    private readonly ProfiqoDbContext _db;

    public TenantRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken cancellationToken)
        => _db.Tenants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        slug = slug.Trim().ToLowerInvariant();
        return _db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        await _db.Tenants.AddAsync(tenant, cancellationToken);
    }
}