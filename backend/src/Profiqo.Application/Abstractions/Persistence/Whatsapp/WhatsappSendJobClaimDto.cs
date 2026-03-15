namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public sealed record WhatsappSendJobClaimDto(
    Guid Id,
    Guid TenantId,
    Guid ConnectionId,
    WhatsappSendJobStatus Status,
    int AttemptCount,
    DateTimeOffset NextAttemptAtUtc,
    string PayloadJson);