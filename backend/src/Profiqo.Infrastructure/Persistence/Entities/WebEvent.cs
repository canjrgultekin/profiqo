namespace Profiqo.Infrastructure.Persistence.Entities;

/// <summary>
/// Storefront event kaydı — web_events tablosuna map'lenir.
/// Lightweight entity, domain aggregate değil (yüksek hacimli write-only).
/// </summary>
public sealed class WebEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string DeviceIdHash { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ClientIp { get; set; }
    public string? PageUrl { get; set; }
    public string? PagePath { get; set; }
    public string? PageReferrer { get; set; }
    public string? PageTitle { get; set; }
    public string? UserAgent { get; set; }
    public string EventDataJson { get; set; } = "{}";
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
