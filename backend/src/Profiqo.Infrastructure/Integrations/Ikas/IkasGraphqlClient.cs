using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Common.Exceptions;

namespace Profiqo.Infrastructure.Integrations.Ikas;

public sealed class IkasOptions
{
    // IMPORTANT: your confirmed endpoint
    public string GraphqlEndpoint { get; init; } = "https://api.myikas.com/api/admin/graphql";
    public int DefaultPageSize { get; init; } = 50;
    public int DefaultMaxPages { get; init; } = 20;
}

internal sealed class IkasGraphqlClient : IIkasGraphqlClient
{
    private readonly HttpClient _http;
    private readonly IkasOptions _opts;

    public IkasGraphqlClient(HttpClient http, IOptions<IkasOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public async Task<string> MeAsync(string accessToken, CancellationToken ct)
    {
        const string op = "me";
        var payload = new
        {
            operationName = op,
            query = @"
query me {
  me { id }
}",
            variables = new { }
        };

        using var doc = await PostAsync(accessToken, op, payload, ct);

        var id = doc.RootElement.GetProperty("data").GetProperty("me").GetProperty("id").GetString();
        return id ?? throw new InvalidOperationException("Ikas me.id missing.");
    }

    public Task<JsonDocument> ListCustomersAsync(string accessToken, int page, int limit, CancellationToken ct)
    {
        const string op = "listCustomer";

        var payload = new
        {
            operationName = op,
            query = @"
query listCustomer($page:Int!, $limit:Int!) {
  listCustomer(pagination: { page: $page, limit: $limit }) {
    data {
      id
      email
      firstName
      lastName
      phone
      createdAt
      updatedAt
    }
  }
}",
            variables = new { page, limit }
        };

        return PostAsync(accessToken, op, payload, ct);
    }

    public Task<JsonDocument> ListOrdersAsync(string accessToken, int page, int limit, CancellationToken ct)
    {
        const string op = "listOrder";

        var payload = new
        {
            operationName = op,
            query = @"
query listOrder($page:Int!, $limit:Int!) {
  listOrder(pagination: { page: $page, limit: $limit }) {
    data {
      id
      orderNumber
      orderedAt
      status
      currencyCode
      totalFinalPrice
      customer { id email firstName lastName phone }
    }
  }
}",
            variables = new { page, limit }
        };

        return PostAsync(accessToken, op, payload, ct);
    }

    private async Task<JsonDocument> PostAsync(string accessToken, string op, object body, CancellationToken ct)
    {
        // EXACT match with your curl style: /graphql?op=me
        var url = QueryHelpers.AddQueryString(_opts.GraphqlEndpoint, "op", op);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var json = JsonSerializer.Serialize(body);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ikas GraphQL HTTP {(int)res.StatusCode}: {text}");

        var doc = JsonDocument.Parse(text);

        if (doc.RootElement.TryGetProperty("errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array &&
            errors.GetArrayLength() > 0)
        {
            // Convert login required to clean domain error
            var first = errors[0];
            var msg = first.TryGetProperty("message", out var m) ? m.GetString() : null;

            if (string.Equals(msg, "LOGIN_REQUIRED", StringComparison.OrdinalIgnoreCase))
            {
                throw new ExternalServiceAuthException(
                    provider: "ikas",
                    message: "Ikas token invalid veya yetkisiz. Admin GraphQL token kullanmalısın (Authorization: Bearer <token>).");
            }

            throw new InvalidOperationException($"Ikas GraphQL errors: {errors}");
        }

        return doc;
    }
}
