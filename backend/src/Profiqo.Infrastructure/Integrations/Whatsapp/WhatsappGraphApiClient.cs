using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Profiqo.Application.Abstractions.Integrations.Whatsapp;

namespace Profiqo.Infrastructure.Integrations.Whatsapp;

internal sealed class WhatsappGraphApiClient : IWhatsappGraphApiClient
{
    private readonly HttpClient _http;
    private readonly WhatsappOptions _opt;

    public WhatsappGraphApiClient(HttpClient http, IOptions<WhatsappOptions> opt)
    {
        _http = http;
        _opt = opt.Value;

        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _opt.HttpTimeoutSeconds));
        _http.BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<WhatsappPhoneNumberInfo> GetPhoneNumberInfoAsync(string phoneNumberId, CancellationToken ct)
    {
        EnsureToken();

        var url = $"{_opt.GraphApiVersion}/{phoneNumberId}?fields=display_phone_number,verified_name";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.SystemUserAccessToken);

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"WhatsApp Graph API error ({(int)res.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var display = root.TryGetProperty("display_phone_number", out var dp) ? dp.GetString() : null;
        var verified = root.TryGetProperty("verified_name", out var vn) ? vn.GetString() : null;

        return new WhatsappPhoneNumberInfo(phoneNumberId, display, verified);
    }

    public async Task<(string? TemplateId, string Status, string RawJson)> CreateMessageTemplateAsync(
        string wabaId,
        string name,
        string language,
        string category,
        JsonElement components,
        CancellationToken ct)
    {
        EnsureToken();

        var url = $"{_opt.GraphApiVersion}/{wabaId}/message_templates";

        using var bodyDoc = BuildCreateTemplatePayload(name, language, category, components);
        var body = bodyDoc.RootElement.GetRawText();

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.SystemUserAccessToken);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Create template failed ({(int)res.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var status = root.TryGetProperty("status", out var st) ? (st.GetString() ?? "UNKNOWN") : "UNKNOWN";

        return (id, status, raw);
    }

    public async Task<IReadOnlyList<WhatsappTemplateRemoteItem>> ListMessageTemplatesAsync(string wabaId, CancellationToken ct)
    {
        EnsureToken();

        var url = $"{_opt.GraphApiVersion}/{wabaId}/message_templates?limit=200";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.SystemUserAccessToken);

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"List templates failed ({(int)res.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return Array.Empty<WhatsappTemplateRemoteItem>();

        var list = new List<WhatsappTemplateRemoteItem>();

        foreach (var item in data.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

            var name = item.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
            var lang = item.TryGetProperty("language", out var l) ? (l.GetString() ?? "") : "";
            var cat = item.TryGetProperty("category", out var c) ? (c.GetString() ?? "") : "";
            var status = item.TryGetProperty("status", out var s) ? (s.GetString() ?? "") : "";
            var rejected = item.TryGetProperty("rejected_reason", out var rr) ? rr.GetString() : null;

            var componentsJson = item.TryGetProperty("components", out var comps)
                ? comps.GetRawText()
                : "[]";

            list.Add(new WhatsappTemplateRemoteItem(
                Id: id,
                Name: name,
                Language: lang,
                Category: cat,
                Status: status,
                RejectedReason: rejected,
                ComponentsJson: componentsJson));
        }

        return list;
    }

    public async Task<(string MessageId, string RawJson)> SendTemplateMessageAsync(
        string phoneNumberId,
        string toPhoneE164,
        string templateName,
        string languageCode,
        JsonElement? components,
        bool useMarketingEndpoint,
        CancellationToken ct)
    {
        EnsureToken();

        var endpoint = useMarketingEndpoint ? "marketing_messages" : "messages";
        var url = $"{_opt.GraphApiVersion}/{phoneNumberId}/{endpoint}";

        using var bodyDoc = BuildSendTemplatePayload(toPhoneE164, templateName, languageCode, components);
        var body = bodyDoc.RootElement.GetRawText();

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.SystemUserAccessToken);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Send template failed ({(int)res.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var messageId = "unknown";
        if (root.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array && msgs.GetArrayLength() > 0)
        {
            var first = msgs[0];
            if (first.TryGetProperty("id", out var idEl))
                messageId = idEl.GetString() ?? "unknown";
        }

        return (messageId, raw);
    }

    private void EnsureToken()
    {
        if (string.IsNullOrWhiteSpace(_opt.SystemUserAccessToken))
            throw new InvalidOperationException("Profiqo:Integrations:Whatsapp:SystemUserAccessToken is missing.");
    }

    private static JsonDocument BuildCreateTemplatePayload(string name, string language, string category, JsonElement components)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("name", name);
            w.WriteString("language", language);
            w.WriteString("category", category);

            w.WritePropertyName("components");
            components.WriteTo(w);

            w.WriteEndObject();
        }
        return JsonDocument.Parse(ms.ToArray());
    }

    private static JsonDocument BuildSendTemplatePayload(string toE164, string templateName, string lang, JsonElement? components)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("messaging_product", "whatsapp");
            w.WriteString("to", toE164);
            w.WriteString("type", "template");

            w.WritePropertyName("template");
            w.WriteStartObject();

            w.WriteString("name", templateName);

            w.WritePropertyName("language");
            w.WriteStartObject();
            w.WriteString("code", lang);
            w.WriteEndObject();

            if (components.HasValue && components.Value.ValueKind == JsonValueKind.Array)
            {
                w.WritePropertyName("components");
                components.Value.WriteTo(w);
            }

            w.WriteEndObject();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray());
    }
}
