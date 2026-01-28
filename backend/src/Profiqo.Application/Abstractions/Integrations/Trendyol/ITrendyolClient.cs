using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Trendyol;

public interface ITrendyolClient
{
    Task<JsonDocument> GetOrdersAsync(
        string apiKey,
        string apiSecret,
        string supplierId,
        int page,
        int size,
        string status,
        long startDateMs,
        long endDateMs,
        CancellationToken ct);
}