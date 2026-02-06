using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Profiqo.Whatsapp.Automation.Worker;

internal sealed class WhatsappCloudSender
{
    private readonly HttpClient _http;

    public WhatsappCloudSender(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://graph.facebook.com/");
        _http.Timeout = TimeSpan.FromSeconds(25);
    }

    // Not: Şimdilik template parametreleri yoksa çalışır. Parametreli template send için components üretimini sonra ekleriz.
    public async Task SendTemplateAsync(
        string accessToken,
        string phoneNumberId,
        string toE164,
        string templateName,
        string languageCode,
        CancellationToken ct)
    {
        using var bodyDoc = BuildTemplateSendPayload(toE164, templateName, languageCode);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"v24.0/{phoneNumberId}/messages");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(bodyDoc.RootElement.GetRawText(), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"WhatsApp send failed ({(int)res.StatusCode}): {raw}");
    }

    private static JsonDocument BuildTemplateSendPayload(string toE164, string templateName, string languageCode)
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
            w.WriteString("code", languageCode);
            w.WriteEndObject();

            w.WriteEndObject();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray());
    }
}
