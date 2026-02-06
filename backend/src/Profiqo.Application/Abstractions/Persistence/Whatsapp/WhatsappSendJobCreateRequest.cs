namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public sealed record WhatsappSendJobCreateRequest(
    Guid TenantId,
    Guid ConnectionId,
    string PayloadJson,
    DateTimeOffset NextAttemptAtUtc);