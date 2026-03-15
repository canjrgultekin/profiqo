namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class WhatsappOrderEventRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string OrderId { get; set; } = "";
    public Guid CustomerId { get; set; }
    public string ToE164 { get; set; } = "";

    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
}