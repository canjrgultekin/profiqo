using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class IngestionEvent
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }

    public ProviderType ProviderType { get; private set; }
    public string EventType { get; private set; } = string.Empty;

    public string ProviderEventId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public bool SignatureValid { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public short ProcessingStatus { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private IngestionEvent() { }

    public IngestionEvent(
        Guid id,
        TenantId tenantId,
        ProviderType providerType,
        string eventType,
        string providerEventId,
        DateTimeOffset occurredAtUtc,
        bool signatureValid,
        string payloadJson)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(providerEventId)) throw new ArgumentException("ProviderEventId is required.", nameof(providerEventId));

        Id = id;
        TenantId = tenantId;
        ProviderType = providerType;
        EventType = eventType;
        ProviderEventId = providerEventId;

        OccurredAtUtc = occurredAtUtc;
        ReceivedAtUtc = DateTimeOffset.UtcNow;

        SignatureValid = signatureValid;
        PayloadJson = payloadJson;

        ProcessingStatus = 0;
        Attempts = 0;
        NextAttemptAtUtc = DateTimeOffset.UtcNow;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkProcessed()
    {
        ProcessingStatus = 1;
        NextAttemptAtUtc = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset nextAttemptAtUtc)
    {
        ProcessingStatus = 2;
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}
