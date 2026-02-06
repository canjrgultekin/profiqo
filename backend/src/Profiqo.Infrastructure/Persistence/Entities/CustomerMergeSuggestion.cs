// Path: backend/src/Profiqo.Infrastructure/Persistence/Entities/CustomerMergeSuggestion.cs
namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class CustomerMergeSuggestion
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public string GroupKey { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Rationale { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    private CustomerMergeSuggestion() { }

    public CustomerMergeSuggestion(
        Guid id,
        Guid tenantId,
        string groupKey,
        decimal confidence,
        string normalizedName,
        string? rationale,
        string payloadJson,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        GroupKey = groupKey;
        Confidence = confidence;
        NormalizedName = normalizedName;
        Rationale = rationale;
        PayloadJson = payloadJson;

        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Update(decimal confidence, string normalizedName, string? rationale, string payloadJson, DateTimeOffset nowUtc, DateTimeOffset expiresAtUtc)
    {
        Confidence = confidence;
        NormalizedName = normalizedName;
        Rationale = rationale;
        PayloadJson = payloadJson;
        UpdatedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
    }
}