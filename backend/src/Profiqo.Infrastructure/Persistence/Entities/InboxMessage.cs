using Profiqo.Domain.Common.Ids;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class InboxMessage
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }

    public string ConsumerName { get; private set; } = string.Empty;
    public string MessageId { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private InboxMessage() { }

    public InboxMessage(Guid id, TenantId tenantId, string consumerName, string messageId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(consumerName)) throw new ArgumentException("ConsumerName is required.", nameof(consumerName));
        if (string.IsNullOrWhiteSpace(messageId)) throw new ArgumentException("MessageId is required.", nameof(messageId));

        Id = id;
        TenantId = tenantId;
        ConsumerName = consumerName;
        MessageId = messageId;
        ProcessedAtUtc = DateTimeOffset.UtcNow;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}