namespace Profiqo.Application.Abstractions.Persistence.Whatsapp;

public sealed record WhatsappSendJobDto(
    Guid Id,
    Guid TenantId,
    Guid ConnectionId,
    WhatsappSendJobStatus Status,
    int AttemptCount,
    DateTimeOffset NextAttemptAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? LastError);