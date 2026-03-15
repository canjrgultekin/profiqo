using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class IntegrationCursorRepository : IIntegrationCursorRepository
{
    private readonly ProfiqoDbContext _db;

    public IntegrationCursorRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetAsync(TenantId tenantId, ProviderConnectionId connectionId, string cursorKey, CancellationToken ct)
    {
        var row = await _db.Set<IntegrationCursor>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProviderConnectionId == connectionId && x.CursorKey == cursorKey)
            .Select(x => x.CursorValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(row) ? null : row;
    }

    public async Task UpsertAsync(TenantId tenantId, ProviderConnectionId connectionId, string cursorKey, string cursorValue, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var existing = await _db.Set<IntegrationCursor>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProviderConnectionId == connectionId && x.CursorKey == cursorKey, ct);

        if (existing is null)
        {
            var created = new IntegrationCursor(
                id: Guid.NewGuid(),
                tenantId: tenantId,
                providerConnectionId: connectionId,
                cursorKey: cursorKey,
                cursorValue: cursorValue,
                updatedAtUtc: nowUtc);

            await _db.Set<IntegrationCursor>().AddAsync(created, ct);
            await _db.SaveChangesAsync(ct);
            return;
        }

        existing.Update(cursorValue, nowUtc);
        await _db.SaveChangesAsync(ct);
    }
}