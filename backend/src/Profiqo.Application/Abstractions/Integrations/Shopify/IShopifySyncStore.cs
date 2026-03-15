// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Shopify/IShopifySyncStore.cs
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Integrations.Shopify;

public sealed record ShopifyCustomerUpsert(
    string ProviderCustomerId, string? FirstName, string? LastName,
    string? EmailNormalized, string? EmailHashSha256,
    string? PhoneNormalized, string? PhoneHashSha256,
    string ProviderCustomerJson);

public sealed record ShopifyOrderLineUpsert(
    string? Sku, string ProductName, int Quantity,
    decimal UnitPrice, decimal FinalPrice, string CurrencyCode,
    string? ProviderVariantId, string? ProviderProductId,
    string? ProductCategory, string? BrandName, string? Barcode,
    decimal Discount, string? OrderLineItemStatusName);

public sealed record ShopifyOrderUpsert(
    string ProviderOrderId, DateTimeOffset PlacedAtUtc,
    string CurrencyCode, decimal TotalFinalPrice,
    string? FinancialStatus, string? FulfillmentStatus,
    string? ProviderCustomerJson,
    string? CustomerEmailNormalized, string? CustomerEmailHashSha256,
    string? CustomerPhoneNormalized, string? CustomerPhoneHashSha256,
    IReadOnlyList<ShopifyOrderLineUpsert> Lines,
    string? ShippingAddressJson, string? BillingAddressJson);

public sealed record ShopifyProductVariantUpsert(
    string ProviderVariantId, string? Sku, string? Barcode,
    decimal? Price, int? InventoryQuantity);

public sealed record ShopifyProductUpsert(
    string ProviderProductId, string Name, string? Description,
    string? Vendor, string? ProductType, int TotalInventory,
    DateTimeOffset ProviderCreatedAt, DateTimeOffset ProviderUpdatedAt,
    IReadOnlyList<ShopifyProductVariantUpsert> Variants);

public interface IShopifySyncStore
{
    Task<CustomerId> UpsertCustomerAsync(TenantId tenantId, ShopifyCustomerUpsert model, CancellationToken ct);
    Task<OrderId> UpsertOrderAsync(TenantId tenantId, ShopifyOrderUpsert model, CancellationToken ct);
    Task UpsertProductAsync(TenantId tenantId, ProviderConnectionId connectionId, ShopifyProductUpsert model, CancellationToken ct);
}