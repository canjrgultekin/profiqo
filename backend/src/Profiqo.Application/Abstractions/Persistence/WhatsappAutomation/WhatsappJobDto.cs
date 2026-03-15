namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public sealed record WhatsappJobDto(
    Guid Id,
    Guid TenantId,
    string Name,
    Guid RuleId,
    Guid Template1Id,
    Guid? Template2Id,
    string TargetsJson,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);