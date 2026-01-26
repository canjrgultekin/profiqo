using Profiqo.Domain.Common.Events;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Domain.Orders.Events;


public sealed record OrderCompletedDomainEvent(
    TenantId TenantId,
    OrderId OrderId,
    CustomerId CustomerId,
    DateTimeOffset CompletedAtUtc) : DomainEventBase(CompletedAtUtc);