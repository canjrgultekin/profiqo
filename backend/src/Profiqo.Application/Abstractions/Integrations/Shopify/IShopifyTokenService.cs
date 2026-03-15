// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Shopify/IShopifyTokenService.cs
namespace Profiqo.Application.Abstractions.Integrations.Shopify;

public sealed record ShopifyTokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);

public interface IShopifyTokenService
{
    /// <summary>
    /// client_credentials grant ile access token alır.
    /// Her mağazanın kendi clientId/clientSecret'ı var, DB'den okunup buraya geçilir.
    /// </summary>
    Task<ShopifyTokenResult> AcquireTokenAsync(string shopName, string clientId, string clientSecret, CancellationToken ct);
}