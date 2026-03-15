using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Integrations.Trendyol;

public interface ITrendyolSyncProcessor
{
    Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
}