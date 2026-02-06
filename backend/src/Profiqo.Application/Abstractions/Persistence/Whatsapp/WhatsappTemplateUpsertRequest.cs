// Path: backend/src/Profiqo.Application/Abstractions/Persistence/Whatsapp/WhatsappTemplateUpsertRequest.cs
namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public sealed record WhatsappTemplateUpsertRequest(
    Guid TenantId,
    Guid ConnectionId,
    string Name,
    string Language,
    string Category,
    string Status,
    string ComponentsJson,
    string? MetaTemplateId,
    string? RejectionReason);