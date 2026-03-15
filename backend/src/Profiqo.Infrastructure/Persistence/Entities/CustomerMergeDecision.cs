using Profiqo.Domain.Common.Ids;

namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class CustomerMergeDecision
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }

    public string GroupKey { get; private set; } = string.Empty;
    public CustomerMergeDecisionStatus Status { get; private set; }

    public DateTimeOffset SuggestionUpdatedAtUtc { get; private set; }

    public Guid? CanonicalCustomerId { get; private set; }

    public DateTimeOffset DecidedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private CustomerMergeDecision() { }

    public CustomerMergeDecision(
        Guid id,
        TenantId tenantId,
        string groupKey,
        CustomerMergeDecisionStatus status,
        Guid? canonicalCustomerId,
        DateTimeOffset suggestionUpdatedAtUtc,
        DateTimeOffset nowUtc)
    {
        Id = id;
        TenantId = tenantId;
        GroupKey = groupKey;
        Status = status;
        CanonicalCustomerId = canonicalCustomerId;
        SuggestionUpdatedAtUtc = suggestionUpdatedAtUtc;

        DecidedAtUtc = nowUtc;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkApproved(Guid canonicalCustomerId, DateTimeOffset suggestionUpdatedAtUtc, DateTimeOffset nowUtc)
    {
        Status = CustomerMergeDecisionStatus.Approved;
        CanonicalCustomerId = canonicalCustomerId;
        SuggestionUpdatedAtUtc = suggestionUpdatedAtUtc;
        DecidedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkRejected(DateTimeOffset suggestionUpdatedAtUtc, DateTimeOffset nowUtc)
    {
        Status = CustomerMergeDecisionStatus.Rejected;
        CanonicalCustomerId = null;
        SuggestionUpdatedAtUtc = suggestionUpdatedAtUtc;
        DecidedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }
}
