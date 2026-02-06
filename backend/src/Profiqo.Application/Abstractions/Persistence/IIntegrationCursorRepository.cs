using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Persistence;

public interface IIntegrationCursorRepository
{
    Task<string?> GetAsync(TenantId tenantId, ProviderConnectionId connectionId, string cursorKey, CancellationToken ct);

    Task UpsertAsync(TenantId tenantId, ProviderConnectionId connectionId, string cursorKey, string cursorValue, DateTimeOffset nowUtc, CancellationToken ct);
}