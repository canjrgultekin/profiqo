// Path: backend/src/Profiqo.Infrastructure/Integrations/Trendyol/TrendyolClient.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Integrations.Trendyol;

namespace Profiqo.Infrastructure.Integrations.Trendyol;

internal sealed class TrendyolClient : ITrendyolClient
{
    private readonly HttpClient _http;
    private readonly TrendyolOptions _opts;

    public TrendyolClient(HttpClient http, IOptions<TrendyolOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public async Task<JsonDocument> GetOrdersAsync(
        string apiKey,
        string apiSecret,
        string sellerId,
        string userAgent,
        long startDateMs,
        long endDateMs,
        int page,
        int size,
        string orderByField,
        CancellationToken ct)
    {
        // https://apigw.trendyol.com/integration/order/sellers/{sellerId}/orders
        var baseUrl = _opts.BaseUrl.TrimEnd('/');
        var prefix = _opts.IntegrationPrefix.TrimEnd('/');

        var safeSize = size <= 0 ? _opts.DefaultPageSize : Math.Min(size, _opts.PageSizeMax);
        var safePage = page < 0 ? 0 : page;

        var url =
            $"{baseUrl}{prefix}/order/sellers/{Uri.EscapeDataString(sellerId)}/orders" +
            $"?startDate={startDateMs}&endDate={endDateMs}" +
            $"&page={safePage}&size={safeSize}" +
            $"&orderByField={Uri.EscapeDataString(string.IsNullOrWhiteSpace(orderByField) ? _opts.OrderByField : orderByField)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        // Basic base64(apiKey:apiSecret)
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // User-Agent zorunlu kabul ediyoruz
        req.Headers.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(userAgent) ? $"Profiqo/{sellerId}" : userAgent);

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Trendyol HTTP {(int)res.StatusCode}: {text}");

        return JsonDocument.Parse(text);
    }
}
