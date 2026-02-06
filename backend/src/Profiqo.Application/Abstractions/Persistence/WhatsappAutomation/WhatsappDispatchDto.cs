namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public sealed record WhatsappDispatchDto(
    Guid Id,
    Guid TenantId,
    Guid JobId,
    Guid RuleId,
    Guid CustomerId,
    string ToE164,
    short MessageNo,
    Guid TemplateId,
    DateTimeOffset PlannedAtUtc,
    DateOnly LocalDate,
    WhatsappDispatchStatus Status,
    int AttemptCount,
    DateTimeOffset NextAttemptAtUtc,
    DateTimeOffset? SentAtUtc,
    string? LastError,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);