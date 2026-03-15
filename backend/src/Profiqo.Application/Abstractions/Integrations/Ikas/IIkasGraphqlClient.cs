// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Ikas/IIkasGraphqlClient.cs
using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Ikas;

public interface IIkasGraphqlClient
{
    Task<string> MeAsync(string storeName, string accessToken, CancellationToken ct);

    Task<JsonDocument> ListCustomersAsync(string storeName, string accessToken, int page, int limit, CancellationToken ct);

    Task<JsonDocument> ListOrdersAsync(string storeName, string accessToken, int page, int limit, long? orderedAtGteMs, CancellationToken ct);

    Task<JsonDocument> ListAbandonedCheckoutsAsync(string storeName, string accessToken, int page, int limit, long? lastActivityGteMs, CancellationToken ct);

    Task<JsonDocument> ListProductsAsync(string storeName, string accessToken, int page, int limit, long? lastActivityGteMs, CancellationToken ct);

    // ── Storefront yönetimi (v2 Admin GraphQL API) ──

    /// <summary>Merchant'ın storefront listesini döner.</summary>
    Task<JsonDocument> ListStorefrontsAsync(string storeName, string accessToken, CancellationToken ct);

    /// <summary>Storefront'a JS script ekler/günceller.</summary>
    Task<JsonDocument> SaveStorefrontJSScriptAsync(
        string storeName,
        string accessToken,
        string storefrontId,
        string scriptContent,
        string scriptName,
        bool isHighPriority,
        CancellationToken ct);
}