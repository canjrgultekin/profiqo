// Path: backend/src/Profiqo.Application/Integrations/Hepsiburada/IHepsiburadaSyncProcessor.cs
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Integrations.Hepsiburada;

public interface IHepsiburadaSyncProcessor
{
    Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
}