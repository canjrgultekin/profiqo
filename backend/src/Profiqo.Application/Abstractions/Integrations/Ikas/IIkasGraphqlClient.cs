using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Ikas;

public interface IIkasGraphqlClient
{
    Task<string> MeAsync(string storeName, string accessToken, CancellationToken ct);

    Task<JsonDocument> ListCustomersAsync(string storeName, string accessToken, int page, int limit, CancellationToken ct);

    Task<JsonDocument> ListOrdersAsync(string storeName, string accessToken, int page, int limit, long? orderedAtGteMs, CancellationToken ct);

    Task<JsonDocument> ListAbandonedCheckoutsAsync(string storeName, string accessToken, int page, int limit, long? lastActivityGteMs, CancellationToken ct);
}