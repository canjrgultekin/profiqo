// Path: backend/src/Profiqo.Application/StorefrontEvents/IStorefrontCheckoutProjector.cs
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.StorefrontEvents;

public interface IStorefrontCheckoutProjector
{
    Task ProjectCompleteCheckoutAsync(
        TenantId tenantId,
        Guid? resolvedCustomerId,
        string eventDataJson,
        DateTimeOffset occurredAtUtc,
        CancellationToken ct);
}