// Path: backend/src/Profiqo.Infrastructure/Integrations/Shopify/ShopifyGraphqlClient.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Integrations.Shopify;

namespace Profiqo.Infrastructure.Integrations.Shopify;

internal sealed class ShopifyGraphqlClient : IShopifyGraphqlClient
{
    private readonly HttpClient _http;
    private readonly ShopifyOptions _opts;
    private readonly ILogger<ShopifyGraphqlClient> _logger;
    private const int MaxRetries = 3;
    private const int BaseRetryMs = 1000;

    public ShopifyGraphqlClient(HttpClient http, IOptions<ShopifyOptions> opts, ILogger<ShopifyGraphqlClient> logger)
    { _http = http; _opts = opts.Value; _logger = logger; }

    public async Task<JsonDocument> QueryAsync(string shopName, string accessToken, string graphqlQuery, object? variables, CancellationToken ct)
    {
        var shop = Normalize(shopName);
        var url = $"https://{shop}.myshopify.com/admin/api/{_opts.ApiVersion}/graphql.json";
        var json = JsonSerializer.Serialize(new { query = graphqlQuery, variables });

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Add("X-Shopify-Access-Token", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Shopify 401: Token invalid/expired. Will auto-refresh on next attempt.");
            if (res.StatusCode == HttpStatusCode.Forbidden)
                throw new InvalidOperationException("Shopify 403: Insufficient scopes. Need read_orders, read_customers, read_products.");
            if (res.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt >= MaxRetries) throw new InvalidOperationException("Shopify 429: Rate limit after max retries.");
                var wait = BaseRetryMs * (int)Math.Pow(2, attempt);
                if (res.Headers.TryGetValues("Retry-After", out var ra) && double.TryParse(ra.FirstOrDefault(), out var sec) && sec > 0 && sec < 30)
                    wait = (int)(sec * 1000);
                _logger.LogWarning("Shopify 429. Retry {A}/{M} after {W}ms", attempt + 1, MaxRetries, wait);
                await Task.Delay(wait, ct);
                continue;
            }

            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Shopify HTTP {(int)res.StatusCode}: {text}");
            return JsonDocument.Parse(text);
        }
        throw new InvalidOperationException("Shopify request failed after retries.");
    }

    private static string Normalize(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        if (s.StartsWith("https://")) s = s["https://".Length..];
        if (s.StartsWith("http://")) s = s["http://".Length..];
        s = s.TrimEnd('/');
        if (s.Contains(".myshopify.com")) s = s.Split('.')[0];
        return s;
    }
}