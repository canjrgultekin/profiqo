// Path: backend/src/Profiqo.Infrastructure/Integrations/Hepsiburada/HepsiburadaClient.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Integrations.Hepsiburada;

namespace Profiqo.Infrastructure.Integrations.Hepsiburada;

internal sealed class HepsiburadaClient : IHepsiburadaClient
{
    private readonly HttpClient _http;
    private readonly HepsiburadaOptions _opts;

    public HepsiburadaClient(HttpClient http, IOptions<HepsiburadaOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public async Task<JsonDocument> GetPaidOrdersAsync(
        string username,
        string password,
        string merchantId,
        int offset,
        int limit,
        string? beginDate,
        string? endDate,
        CancellationToken ct)
    {
        // GET /orders/merchantid/{merchantId}?offset={offset}&limit={limit}&begindate={begin}&enddate={end}
        var baseUrl = _opts.BaseUrl.TrimEnd('/');
        var safeLimit = limit <= 0 ? _opts.DefaultLimit : Math.Min(limit, _opts.LimitMax);
        var safeOffset = offset < 0 ? 0 : offset;

        var url = $"{baseUrl}/orders/merchantid/{Uri.EscapeDataString(merchantId)}?offset={safeOffset}&limit={safeLimit}";

        if (!string.IsNullOrWhiteSpace(beginDate))
            url += $"&begindate={Uri.EscapeDataString(beginDate)}";
        if (!string.IsNullOrWhiteSpace(endDate))
            url += $"&enddate={Uri.EscapeDataString(endDate)}";

        return await SendGetAsync(username, password, url, ct);
    }

    public async Task<JsonDocument> GetPackagesAsync(
        string username,
        string password,
        string merchantId,
        int offset,
        int limit,
        string? beginDate,
        string? endDate,
        CancellationToken ct)
    {
        // GET /packages/merchantid/{merchantId}?offset={offset}&limit={limit}&begindate={begin}&enddate={end}
        var baseUrl = _opts.BaseUrl.TrimEnd('/');
        var safeLimit = limit <= 0 ? _opts.PackageLimitMax : Math.Min(limit, _opts.PackageLimitMax);
        var safeOffset = offset < 0 ? 0 : offset;

        var url = $"{baseUrl}/packages/merchantid/{Uri.EscapeDataString(merchantId)}?offset={safeOffset}&limit={safeLimit}";

        if (!string.IsNullOrWhiteSpace(beginDate))
            url += $"&begindate={Uri.EscapeDataString(beginDate)}";
        if (!string.IsNullOrWhiteSpace(endDate))
            url += $"&enddate={Uri.EscapeDataString(endDate)}";

        return await SendGetAsync(username, password, url, ct);
    }

    public async Task<JsonDocument> GetOrderDetailAsync(
        string username,
        string password,
        string merchantId,
        string orderNumber,
        CancellationToken ct)
    {
        // GET /orders/merchantid/{merchantId}/ordernumber/{orderNumber}
        var baseUrl = _opts.BaseUrl.TrimEnd('/');

        var url = $"{baseUrl}/orders/merchantid/{Uri.EscapeDataString(merchantId)}/ordernumber/{Uri.EscapeDataString(orderNumber)}";

        return await SendGetAsync(username, password, url, ct);
    }

    private async Task<JsonDocument> SendGetAsync(string username, string password, string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        // HB Basic Auth: base64(username:password)
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Hepsiburada HTTP {(int)res.StatusCode}: {text}");

        return JsonDocument.Parse(text);
    }
}