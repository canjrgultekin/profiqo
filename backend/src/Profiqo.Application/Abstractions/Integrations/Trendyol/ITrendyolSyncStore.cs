// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Trendyol/ITrendyolSyncStore.cs
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Abstractions.Integrations.Trendyol;

public sealed record TrendyolOrderLineUpsert(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string CurrencyCode);

public sealed record TrendyolOrderUpsert(
    string ShipmentPackageId,
    string OrderNumber,
    DateTimeOffset OrderDateUtc,
    string CurrencyCode,
    decimal TotalPrice,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CustomerFirstName,
    string? CustomerLastName,
    IReadOnlyList<TrendyolOrderLineUpsert> Lines,
    string PayloadJson);

public interface ITrendyolSyncStore
{
    Task UpsertOrderAsync(TenantId tenantId, TrendyolOrderUpsert model, CancellationToken ct);
}