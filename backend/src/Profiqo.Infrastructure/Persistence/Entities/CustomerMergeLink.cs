using Profiqo.Domain.Common.Ids;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class CustomerMergeLink
{
    public TenantId TenantId { get; private set; }

    public CustomerId SourceCustomerId { get; private set; }
    public CustomerId CanonicalCustomerId { get; private set; }

    public string GroupKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private CustomerMergeLink() { }

    public CustomerMergeLink(TenantId tenantId, CustomerId sourceCustomerId, CustomerId canonicalCustomerId, string groupKey, DateTimeOffset nowUtc)
    {
        TenantId = tenantId;
        SourceCustomerId = sourceCustomerId;
        CanonicalCustomerId = canonicalCustomerId;
        GroupKey = groupKey;

        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void PointTo(CustomerId canonicalCustomerId, string groupKey, DateTimeOffset nowUtc)
    {
        CanonicalCustomerId = canonicalCustomerId;
        GroupKey = groupKey;
        UpdatedAtUtc = nowUtc;
    }
}