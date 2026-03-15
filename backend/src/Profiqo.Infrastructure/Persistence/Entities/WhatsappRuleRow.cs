using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class WhatsappRuleRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";

    public WhatsappRuleMode Mode { get; set; }
    public short DailyLimit { get; set; }
    public string Timezone { get; set; } = "Europe/Istanbul";

    public TimeOnly? DailyTime1 { get; set; }
    public TimeOnly? DailyTime2 { get; set; }
    public int? DailyDelay2Minutes { get; set; }

    public int? OrderDelay1Minutes { get; set; }
    public int? OrderDelay2Minutes { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}