namespace Profiqo.Domain.Common.Events;

public abstract record DomainEventBase : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; }

    protected DomainEventBase(DateTimeOffset occurredAtUtc)
    {
        OccurredAtUtc = Guard.EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
    }
}