// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Ikas/IIkasSyncStore.cs
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Integrations.Ikas;

public sealed record IkasCustomerUpsert(
    string ProviderCustomerId,
    string? FirstName,
    string? LastName,
    string? EmailNormalized,
    string? EmailHashSha256,
    string? PhoneNormalized,
    string? PhoneHashSha256);

public sealed record IkasOrderLineUpsert(
    string? Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal FinalPrice,
    string CurrencyCode,
    string? ProviderVariantId,
    string? ProviderProductId);

public sealed record IkasOrderUpsert(
    string ProviderOrderId,
    DateTimeOffset PlacedAtUtc,
    long UpdatedAtMs,
    string CurrencyCode,
    decimal TotalFinalPrice,
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

public interface IIkasSyncStore
{
    Task<CustomerId> UpsertCustomerAsync(TenantId tenantId, IkasCustomerUpsert model, CancellationToken ct);

    Task<OrderId> UpsertOrderAsync(TenantId tenantId, IkasOrderUpsert model, CancellationToken ct);

    Task UpsertAbandonedCheckoutAsync(TenantId tenantId, ProviderConnectionId connectionId, IkasAbandonedCheckoutUpsert model, CancellationToken ct);
}