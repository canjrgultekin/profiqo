using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Integrations.Ikas;

public sealed record IkasCustomerUpsert(
    string ProviderCustomerId,
    string? FirstName,
    string? LastName,
    string? EmailNormalized,
    string? EmailHashSha256,
    string? PhoneNormalized,
    string? PhoneHashSha256,
    string ProviderCustomerJson);

public sealed record IkasOrderLineUpsert(
    string? Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal FinalPrice,
    string CurrencyCode,
    string? ProviderVariantId,
    string? ProviderProductId,
    string? ProductCategory,
    string? BrandName,
    IReadOnlyList<string> CategoryNames,
    string? Barcode,
    decimal Discount,
    string? OrderLineItemStatusName);

public sealed record IkasOrderUpsert(
    string ProviderOrderId,
    DateTimeOffset PlacedAtUtc,
    long UpdatedAtMs,
    string CurrencyCode,
    decimal TotalFinalPrice,
    string? OrderStatus, // ✅ NEW: Ikas order status string
    string? ProviderCustomerJson,
    string? CustomerEmailNormalized,
    string? CustomerEmailHashSha256,
    string? CustomerPhoneNormalized,
    string? CustomerPhoneHashSha256,
    IReadOnlyList<IkasOrderLineUpsert> Lines,
    string? ShippingAddressJson,
    string? BillingAddressJson);

public sealed record IkasAbandonedCheckoutUpsert(
    string ExternalId,
    long LastActivityDateMs,
    string? CurrencyCode,
    decimal? TotalFinalPrice,
    string? Status,
    string? CustomerEmail,
    string? CustomerPhone,
    string PayloadJson);
public sealed record IkasProductVariantUpsert(
    string ProviderVariantId,
    string? Sku,
    string? HsCode,
    string? BarcodeListJson,
    bool? SellIfOutOfStock,
    string PricesJson,
    string StocksJson,
    long ProviderCreatedAtMs);

public sealed record IkasProductUpsert(
    string ProviderProductId,
    string Name,
    string? Description,
    string? BrandId,
    string? BrandName,
    string? CategoryIdsJson,
    string? CategoriesJson,
    int TotalStock,
    string? ProductVolumeDiscountId,
    long ProviderCreatedAtMs,
    long ProviderUpdatedAtMs,
    IReadOnlyList<IkasProductVariantUpsert> Variants);

public interface IIkasSyncStore
{
    Task<CustomerId> UpsertCustomerAsync(TenantId tenantId, IkasCustomerUpsert model, CancellationToken ct);
    Task<OrderId> UpsertOrderAsync(TenantId tenantId, IkasOrderUpsert model, CancellationToken ct);
    Task UpsertAbandonedCheckoutAsync(TenantId tenantId, ProviderConnectionId connectionId, IkasAbandonedCheckoutUpsert model, CancellationToken ct);
    Task UpsertProductAsync(TenantId tenantId, ProviderConnectionId connectionId, IkasProductUpsert model, CancellationToken ct);
}