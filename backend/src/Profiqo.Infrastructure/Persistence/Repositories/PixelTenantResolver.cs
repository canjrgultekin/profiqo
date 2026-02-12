using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Profiqo.Application.StorefrontEvents;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Infrastructure.Persistence.Repositories;

/// <summary>
/// publicApiKey ile tenant çözümleme.
/// provider_connections tablosunda ProviderType.Pixel kaydının
/// external_account_id alanını publicApiKey olarak kullanır.
/// 5dk IMemoryCache ile cache'ler.
/// </summary>
public sealed class PixelTenantResolver : IPixelTenantResolver
{
    private readonly ProfiqoDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PixelTenantResolver> _logger;

    private static readonly TimeSpan ValidCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InvalidCacheDuration = TimeSpan.FromSeconds(30);

    public PixelTenantResolver(ProfiqoDbContext db, IMemoryCache cache, ILogger<PixelTenantResolver> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ResolvedPixelTenant?> ResolveAsync(string publicApiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(publicApiKey))
            return null;

        var cacheKey = $"pixel_tenant:{publicApiKey}";

        if (_cache.TryGetValue(cacheKey, out ResolvedPixelTenant? cached))
            return cached;

        // provider_connections tablosunda ProviderType.Pixel kaydını ara
        // external_account_id = publicApiKey, status = Active
        var pixelProviderType = (short)ProviderType.Pixel;

        var result = await _db.Database
            .SqlQueryRaw<PixelTenantRow>(
                """
                SELECT
                    pc.tenant_id    AS tenant_id,
                    t.name          AS tenant_name,
                    pc.display_name AS store_domain
                FROM provider_connections pc
                INNER JOIN tenants t ON t.id = pc.tenant_id
                WHERE pc.external_account_id = {0}
                  AND pc.provider_type = {1}
                  AND pc.status = 1
                """,
                publicApiKey,
                pixelProviderType)
            .FirstOrDefaultAsync(ct);

        if (result is null)
        {
            _logger.LogDebug("Pixel tenant not found for apiKey prefix {KeyPrefix}",
                publicApiKey.Length > 12 ? publicApiKey[..12] + "..." : publicApiKey);

            _cache.Set(cacheKey, (ResolvedPixelTenant?)null, InvalidCacheDuration);
            return null;
        }

        var resolved = new ResolvedPixelTenant
        {
            TenantId = new TenantId(result.TenantId),
            TenantName = result.TenantName ?? "Unknown",
            StoreDomain = result.StoreDomain,
            EnabledEvents =
            [
                "ADD_TO_CART", "REMOVE_FROM_CART", "COMPLETE_CHECKOUT", "ADD_TO_WISHLIST",
                "PAGE_VIEW", "PRODUCT_VIEW", "BEGIN_CHECKOUT", "SEARCH"
            ]
        };

        _cache.Set(cacheKey, resolved, ValidCacheDuration);
        return resolved;
    }

    // Raw SQL query result mapping
    // UseSnakeCaseNamingConvention aktif olduğu için
    // EF property'leri snake_case column adlarıyla eşleştirir:
    // TenantId → tenant_id, TenantName → tenant_name, StoreDomain → store_domain
    private sealed class PixelTenantRow
    {
        public Guid TenantId { get; set; }
        public string? TenantName { get; set; }
        public string? StoreDomain { get; set; }
    }
}