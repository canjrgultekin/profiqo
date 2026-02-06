namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class RawEvent
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public short ProviderType { get; private set; } // Domain ProviderType => short
    public string EventType { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private RawEvent() { }

    public RawEvent(
        Guid id,
        Guid tenantId,
        short providerType,
        string eventType,
        string externalId,
        DateTimeOffset occurredAtUtc,
        string payloadJson,
        DateTimeOffset nowUtc)
    {
        Id = id;
        TenantId = tenantId;
        ProviderType = providerType;
        EventType = eventType;
        ExternalId = externalId;
        OccurredAtUtc = occurredAtUtc;
        PayloadJson = payloadJson;
        CreatedAtUtc = nowUtc;
    }
}