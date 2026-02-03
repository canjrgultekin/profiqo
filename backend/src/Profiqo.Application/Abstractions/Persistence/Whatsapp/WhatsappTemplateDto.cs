// Path: backend/src/Profiqo.Application/Abstractions/Persistence/Whatsapp/WhatsappTemplateDto.cs
namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public sealed record WhatsappTemplateDto(
    Guid Id,
    Guid TenantId,
    Guid ConnectionId,
    string Name,
    string Language,
    string Category,
    string Status,
    string ComponentsJson,
    string? MetaTemplateId,
    string? RejectionReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);