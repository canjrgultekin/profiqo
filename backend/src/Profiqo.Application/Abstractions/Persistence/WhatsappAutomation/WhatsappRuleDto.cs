namespace Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

public sealed record WhatsappRuleDto(
    Guid Id,
    Guid TenantId,
    string Name,
    WhatsappRuleMode Mode,
    short DailyLimit,
    string Timezone,
    TimeOnly? DailyTime1,
    TimeOnly? DailyTime2,
    int? DailyDelay2Minutes,
    int? OrderDelay1Minutes,
    int? OrderDelay2Minutes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);