// Path: backend/src/Profiqo.Infrastructure/Integrations/Shopify/ShopifyTokenService.cs
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Profiqo.Application.Abstractions.Integrations.Shopify;

namespace Profiqo.Infrastructure.Integrations.Shopify;

internal sealed class ShopifyTokenService : IShopifyTokenService
{
    private readonly HttpClient _http;
    private readonly ILogger<ShopifyTokenService> _logger;

    public ShopifyTokenService(HttpClient http, ILogger<ShopifyTokenService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ShopifyTokenResult> AcquireTokenAsync(string shopName, string clientId, string clientSecret, CancellationToken ct)
    {
        var normalizedShop = NormalizeShopName(shopName);
        var url = $"https://{normalizedShop}.myshopify.com/admin/oauth/access_token";

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Shopify ClientId is empty.");
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Shopify ClientSecret is empty.");

        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = formData;
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var res = await _http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Shopify token failed. HTTP {StatusCode}: {Body}, shop={Shop}", (int)res.StatusCode, text, normalizedShop);

            if ((int)res.StatusCode is 401 or 403)
                throw new InvalidOperationException(
                    $"Shopify token failed (HTTP {(int)res.StatusCode}). " +
                    "App not installed on store, or ClientId/ClientSecret wrong, or required scopes not granted.");

            throw new InvalidOperationException($"Shopify token HTTP {(int)res.StatusCode}: {text}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException($"Shopify returned empty access_token. Response: {text}");

        var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.ValueKind == JsonValueKind.Number ? ei.GetInt64() : 86399L;
        var expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

        _logger.LogInformation("Shopify token acquired. shop={Shop}, expiresIn={ExpiresIn}s", normalizedShop, expiresIn);

        return new ShopifyTokenResult(accessToken, expiresAtUtc);
    }

    private static string NormalizeShopName(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        if (s.StartsWith("https://")) s = s["https://".Length..];
        if (s.StartsWith("http://")) s = s["http://".Length..];
        s = s.TrimEnd('/');
        if (s.Contains(".myshopify.com")) s = s.Split('.')[0];
        return s;
    }
}