// Path: backend/src/Profiqo.Application/Abstractions/Integrations/Trendyol/ITrendyolClient.cs
using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Trendyol;

public interface ITrendyolClient
{
    Task<JsonDocument> GetOrdersAsync(
        string apiKey,
        string apiSecret,
        string sellerId,
        string userAgent,
        long startDateMs,
        long endDateMs,
        int page,
        int size,
        string orderByField,
        CancellationToken ct);
}