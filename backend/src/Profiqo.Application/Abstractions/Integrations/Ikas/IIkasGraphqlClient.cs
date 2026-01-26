using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Ikas;

public interface IIkasGraphqlClient
{
    Task<string> MeAsync(string accessToken, CancellationToken ct);

    Task<JsonDocument> ListCustomersAsync(string accessToken, int page, int limit, CancellationToken ct);

    Task<JsonDocument> ListOrdersAsync(string accessToken, int page, int limit, CancellationToken ct);
}