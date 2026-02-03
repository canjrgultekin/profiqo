using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class WhatsappDispatchQueueRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid JobId { get; set; }
    public Guid RuleId { get; set; }

    public Guid CustomerId { get; set; }
    public string ToE164 { get; set; } = "";

    public short MessageNo { get; set; }
    public Guid TemplateId { get; set; }

    public DateTimeOffset PlannedAtUtc { get; set; }
    public DateOnly LocalDate { get; set; }

    public WhatsappDispatchStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }

    public string? LockedBy { get; set; }
    public DateTimeOffset? LockedAtUtc { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset? SentAtUtc { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool IsSimulated { get; set; }

}