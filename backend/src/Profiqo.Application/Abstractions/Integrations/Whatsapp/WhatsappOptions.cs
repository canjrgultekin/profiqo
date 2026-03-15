namespace Profiqo.Application.Abstractions.Integrations.Whatsapp;

public sealed class WhatsappOptions
{
    public string BaseUrl { get; init; } = "https://graph.facebook.com";
    public string GraphApiVersion { get; init; } = "v24.0";

    // Cloud API: System User access token (server-to-server)
    public string SystemUserAccessToken { get; init; } = string.Empty;

    // /messages default. Eğer ileride marketing_messages kullanmak istersen burayı true yaparsın.
    public bool UseMarketingMessagesEndpoint { get; init; } = false;

    public int HttpTimeoutSeconds { get; init; } = 30;
}