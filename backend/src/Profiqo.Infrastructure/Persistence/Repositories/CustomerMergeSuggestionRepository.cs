using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Customers.Dedupe;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class CustomerMergeSuggestionRepository : ICustomerMergeSuggestionRepository
{
    private readonly ProfiqoDbContext _db;

    public CustomerMergeSuggestionRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task UpsertBatchAsync(
        TenantId tenantId,
        IReadOnlyList<CustomerMergeSuggestionUpsert> suggestions,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct)
    {
        if (suggestions.Count == 0) return;

        var set = _db.Set<CustomerMergeSuggestion>();

        foreach (var s in suggestions)
        {
            var groupKey = (s.GroupKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(groupKey)) continue;

            var existing = await set.FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                x.GroupKey == groupKey, ct);

            var conf = (decimal)Math.Max(0, Math.Min(1, s.Confidence));

            if (existing is null)
            {
                await set.AddAsync(new CustomerMergeSuggestion(
                    id: Guid.NewGuid(),
                    tenantId: tenantId.Value,
                    groupKey: groupKey,
                    confidence: conf,
                    normalizedName: s.NormalizedName ?? groupKey,
                    rationale: s.Rationale,
                    payloadJson: s.PayloadJson ?? "{}",
                    nowUtc: nowUtc,
                    expiresAtUtc: expiresAtUtc), ct);
            }
            else
            {
                existing.Update(
                    confidence: conf,
                    normalizedName: s.NormalizedName ?? groupKey,
                    rationale: s.Rationale,
                    payloadJson: s.PayloadJson ?? "{}",
                    nowUtc: nowUtc,
                    expiresAtUtc: expiresAtUtc);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerMergeSuggestionListItemDto>> ListLatestAsync(TenantId tenantId, int take, CancellationToken ct)
    {
        take = take is < 1 or > 500 ? 50 : take;

        var now = DateTimeOffset.UtcNow;

        return await _db.Set<CustomerMergeSuggestion>().AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(take)
            .Select(x => new CustomerMergeSuggestionListItemDto(
                x.Id,
                x.GroupKey,
                (double)x.Confidence,
                x.NormalizedName,
                x.UpdatedAtUtc,
                x.ExpiresAtUtc))
            .ToListAsync(ct);
    }

    public async Task<CustomerMergeSuggestionDetailDto?> GetByGroupKeyAsync(TenantId tenantId, string groupKey, CancellationToken ct)
    {
        groupKey = (groupKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(groupKey)) return null;

        var now = DateTimeOffset.UtcNow;

        var x = await _db.Set<CustomerMergeSuggestion>().AsNoTracking()
            .Where(s => s.TenantId == tenantId.Value && s.GroupKey == groupKey && s.ExpiresAtUtc > now)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (x is null) return null;

        return new CustomerMergeSuggestionDetailDto(
            x.Id,
            x.GroupKey,
            (double)x.Confidence,
            x.NormalizedName,
            x.Rationale ?? "",
            x.PayloadJson,
            x.UpdatedAtUtc,
            x.ExpiresAtUtc);
    }
}
