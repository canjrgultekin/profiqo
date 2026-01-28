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
        string supplierId,
        int page,
        int size,
        string status,
        long startDateMs,
        long endDateMs,
        CancellationToken ct)
    {
        var baseUrl = _opts.BaseUrl.TrimEnd('/');
        var url =
            $"{baseUrl}/suppliers/{Uri.EscapeDataString(supplierId)}/orders" +
            $"?status={Uri.EscapeDataString(status)}&page={page}&size={size}&startDate={startDateMs}&endDate={endDateMs}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Many gateways are picky about UA
        req.Headers.UserAgent.ParseAdd("Profiqo/1.0");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Trendyol HTTP {(int)res.StatusCode}: {text}");

        return JsonDocument.Parse(text);
    }
}