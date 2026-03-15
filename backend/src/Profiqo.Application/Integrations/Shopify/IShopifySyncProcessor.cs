using Profiqo.Domain.Common.Ids;
namespace Profiqo.Application.Integrations.Shopify;

public interface IShopifySyncProcessor
{
    Task<int> SyncCustomersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
    Task<int> SyncOrdersAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
    Task<int> SyncProductsAsync(Guid jobId, TenantId tenantId, Guid connectionId, int pageSize, int maxPages, CancellationToken ct);
}