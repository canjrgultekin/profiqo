// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Shopify/IShopifyGraphqlClient.cs
using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Shopify;

public interface IShopifyGraphqlClient
{
    Task<JsonDocument> QueryAsync(
        string shopName,
        string accessToken,
        string graphqlQuery,
        object? variables,
        CancellationToken ct);
}