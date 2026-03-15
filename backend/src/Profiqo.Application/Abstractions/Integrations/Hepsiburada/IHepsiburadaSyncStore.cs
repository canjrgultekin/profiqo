// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Hepsiburada/IHepsiburadaSyncStore.cs
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Integrations.Hepsiburada;

public sealed record HepsiburadaOrderLineUpsert(
    string Sku,
    string? MerchantSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string CurrencyCode,
    decimal Vat,
    decimal? VatRate,
    decimal Discount,
    decimal TotalPrice,
    string? OrderLineItemStatusName);

public sealed record HepsiburadaAddressDto(
    string? AddressDetail,
    string? City,
    string? Town,
    string? District,
    string? CountryCode,
    string? PostalCode,
    string? Phone,
    string? Email,
    string? FullName);

public sealed record HepsiburadaOrderUpsert(
    string OrderNumber,
    DateTimeOffset OrderDateUtc,
    string CurrencyCode,
    decimal TotalPrice,
    string? OrderStatus,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CustomerId,
    IReadOnlyList<HepsiburadaOrderLineUpsert> Lines,
    string PayloadJson,
    HepsiburadaAddressDto? ShippingAddress,
    HepsiburadaAddressDto? BillingAddress);

public interface IHepsiburadaSyncStore
{
    Task UpsertOrderAsync(TenantId tenantId, HepsiburadaOrderUpsert model, CancellationToken ct);
}