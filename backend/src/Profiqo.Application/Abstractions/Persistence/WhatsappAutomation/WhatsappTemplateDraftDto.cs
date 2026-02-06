namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public sealed record WhatsappTemplateDraftDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Language,
    string Category,
    string Status,
    string ComponentsJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);