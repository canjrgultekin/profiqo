using Profiqo.Domain.Common.Ids;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string MessageType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = "{}";
    public string HeadersJson { get; private set; } = "{}";

    public short Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(
        Guid id,
        TenantId tenantId,
        DateTimeOffset occurredAtUtc,
        string messageType,
        string payloadJson,
        string headersJson)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(messageType)) throw new ArgumentException("MessageType is required.", nameof(messageType));

        Id = id;
        TenantId = tenantId;
        OccurredAtUtc = occurredAtUtc;
        MessageType = messageType;
        PayloadJson = payloadJson;
        HeadersJson = headersJson;

        Status = 0;
        Attempts = 0;
        NextAttemptAtUtc = occurredAtUtc;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkAttemptFailed(string error, DateTimeOffset nextAttemptAtUtc)
    {
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error;
        NextAttemptAtUtc = nextAttemptAtUtc;
        Status = 1;
    }

    public void MarkCompleted()
    {
        Status = 2;
        NextAttemptAtUtc = null;
        LastError = null;
    }
}