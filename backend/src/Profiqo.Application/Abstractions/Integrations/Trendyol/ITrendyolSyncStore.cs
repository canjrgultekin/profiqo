using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Integrations.Trendyol;

public sealed record TrendyolOrderLineUpsert(
    string? Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string CurrencyCode);

public sealed record TrendyolOrderUpsert(
    string ProviderOrderId,
    DateTimeOffset PlacedAtUtc,
    string CurrencyCode,
    decimal TotalAmount,
    string? CustomerEmail,
    string? CustomerPhone,
    IReadOnlyList<TrendyolOrderLineUpsert> Lines,
    string PayloadJson);

public interface ITrendyolSyncStore
{
    Task UpsertOrderAsync(TenantId tenantId, TrendyolOrderUpsert order, CancellationToken ct);
}