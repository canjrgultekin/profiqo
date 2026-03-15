using System.Net.Http.Headers;
using System.Text.Json;

using Profiqo.Application.Abstractions.Integrations.Whatsapp;

namespace Profiqo.Infrastructure.Integrations.Whatsapp;

internal sealed class WhatsappCloudValidator : IWhatsappCloudValidator
{
    private readonly HttpClient _http;

    public WhatsappCloudValidator(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://graph.facebook.com/");
        _http.Timeout = TimeSpan.FromSeconds(25);
    }

    public async Task<(bool ok, string? verifiedName, string? displayPhone, string? rawError)> ValidateAsync(
        string accessToken,
        string phoneNumberId,
        CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"v24.0/{phoneNumberId}?fields=display_phone_number,verified_name");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var res = await _http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return (false, null, null, raw);

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var display = root.TryGetProperty("display_phone_number", out var dp) ? dp.GetString() : null;
            var verified = root.TryGetProperty("verified_name", out var vn) ? vn.GetString() : null;

            return (true, verified, display, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }
}