using System.Text.Json;

namespace Profiqo.Application.Abstractions.Integrations.Whatsapp;

public interface IWhatsappGraphApiClient
{
    Task<WhatsappPhoneNumberInfo> GetPhoneNumberInfoAsync(string phoneNumberId, CancellationToken ct);

    Task<(string? TemplateId, string Status, string RawJson)> CreateMessageTemplateAsync(
        string wabaId,
        string name,
        string language,
        string category,
        JsonElement components,
        CancellationToken ct);

    Task<IReadOnlyList<WhatsappTemplateRemoteItem>> ListMessageTemplatesAsync(
        string wabaId,
        CancellationToken ct);

    Task<(string MessageId, string RawJson)> SendTemplateMessageAsync(
        string phoneNumberId,
        string toPhoneE164,
        string templateName,
        string languageCode,
        JsonElement? components,
        bool useMarketingEndpoint,
        CancellationToken ct);
}