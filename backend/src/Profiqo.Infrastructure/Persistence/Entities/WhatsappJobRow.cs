namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class WhatsappJobRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";

    public Guid RuleId { get; set; }
    public Guid Template1Id { get; set; }
    public Guid? Template2Id { get; set; }

    public string TargetsJson { get; set; } = "[]";

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}