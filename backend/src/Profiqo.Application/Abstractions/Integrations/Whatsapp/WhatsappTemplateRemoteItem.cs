namespace Profiqo.Application.Abstractions.Integrations.Whatsapp;

public sealed record WhatsappTemplateRemoteItem(
    string? Id,
    string Name,
    string Language,
    string Category,
    string Status,
    string? RejectedReason,
    string ComponentsJson);