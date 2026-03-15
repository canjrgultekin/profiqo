using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Integrations.Trendyol;

public sealed record TrendyolOrderLineUpsert(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string CurrencyCode,
    string? ProductCategoryId,
    string? Barcode,
    decimal Discount,
    string? OrderLineItemStatusName);

public sealed record TrendyolAddressDto(
    string? Address1,
    string? Address2,
    string? City,
    int? CityCode,
    string? District,
    int? DistrictId,
    string? CountryCode,
    string? PostalCode,
    string? Phone,
    string? FullName);

public sealed record TrendyolOrderUpsert(
    string ShipmentPackageId,
    string OrderNumber,
    DateTimeOffset OrderDateUtc,
    string CurrencyCode,
    decimal TotalPrice,
    string? OrderStatus, // ✅ NEW: Trendyol order status string
    string? CustomerEmail,
    string? CustomerPhone,
    string? CustomerFirstName,
    string? CustomerLastName,
    IReadOnlyList<TrendyolOrderLineUpsert> Lines,
    string PayloadJson,
    TrendyolAddressDto? ShippingAddress,
    TrendyolAddressDto? BillingAddress);

public interface ITrendyolSyncStore
{
    Task UpsertOrderAsync(TenantId tenantId, TrendyolOrderUpsert model, CancellationToken ct);
}