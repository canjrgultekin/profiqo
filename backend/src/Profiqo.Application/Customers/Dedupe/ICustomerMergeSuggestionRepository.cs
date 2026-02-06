using Profiqo.Domain.Common.Ids;

namespace Profiqo.Application.Customers.Dedupe;

public interface ICustomerMergeSuggestionRepository
{
    Task UpsertBatchAsync(
        TenantId tenantId,
        IReadOnlyList<CustomerMergeSuggestionUpsert> suggestions,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct);

    Task<IReadOnlyList<CustomerMergeSuggestionListItemDto>> ListLatestAsync(
        TenantId tenantId,
        int take,
        CancellationToken ct);

    Task<CustomerMergeSuggestionDetailDto?> GetByGroupKeyAsync(
        TenantId tenantId,
        string groupKey,
        CancellationToken ct);
}

public sealed record CustomerMergeSuggestionUpsert(
    string GroupKey,
    double Confidence,
    string NormalizedName,
    string Rationale,
    string PayloadJson);

public sealed record CustomerMergeSuggestionListItemDto(
    Guid Id,
    string GroupKey,
    double Confidence,
    string NormalizedName,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record CustomerMergeSuggestionDetailDto(
    Guid Id,
    string GroupKey,
    double Confidence,
    string NormalizedName,
    string Rationale,
    string PayloadJson,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset ExpiresAtUtc);