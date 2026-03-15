// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Shopify/ShopifyOptions.cs
namespace Profiqo.Application.Abstractions.Integrations.Shopify;

public sealed class ShopifyOptions
{
    /// <summary>Shopify GraphQL Admin API version.</summary>
    public string ApiVersion { get; init; } = "2026-01";

    public int DefaultPageSize { get; init; } = 50;
    public int DefaultMaxPages { get; init; } = 100;

    /// <summary>Shopify default son 60 gün sipariş verir.</summary>
    public int BackfillDays { get; init; } = 60;

    /// <summary>Token expire olmadan kaç dakika önce yenilensin.</summary>
    public int TokenRefreshBufferMinutes { get; init; } = 30;
}