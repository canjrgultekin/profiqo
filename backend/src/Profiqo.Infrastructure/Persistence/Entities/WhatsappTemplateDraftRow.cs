namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class WhatsappTemplateDraftRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";
    public string Language { get; set; } = "tr";
    public string Category { get; set; } = "MARKETING";
    public string Status { get; set; } = "DRAFT";

    public string ComponentsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}