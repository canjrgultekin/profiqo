using System.Text.Json;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Customers.Dedupe;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/customers/dedupe")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class CustomerDedupeController : ControllerBase
{
    private static readonly JsonSerializerOptions SuggestionPayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISender _sender;
    private readonly ITenantContext _tenant;
    private readonly ICustomerMergeSuggestionRepository _suggestions;
    private readonly ProfiqoDbContext _db;

    public CustomerDedupeController(
        ISender sender,
        ITenantContext tenant,
        ICustomerMergeSuggestionRepository suggestions,
        ProfiqoDbContext db)
    {
        _sender = sender;
        _tenant = tenant;
        _suggestions = suggestions;
        _db = db;
    }

    public sealed record AnalyzeRequest(double? Threshold);

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request, CancellationToken ct)
    {
        var threshold = request.Threshold;

        var result = await _sender.Send(
            new AnalyzeCustomerDuplicatesQuery(threshold ?? 0.78),
            ct);

        return Ok(result);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> ListPending([FromQuery] int take = 100, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest("Missing tenant context");

        if (take <= 0) take = 100;
        if (take > 500) take = 500;

        var nowUtc = DateTimeOffset.UtcNow;

        var activeSuggestions = await _db.Set<CustomerMergeSuggestion>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value.Value && x.ExpiresAtUtc > nowUtc)
            .Select(x => new { x.GroupKey, x.UpdatedAtUtc })
            .ToListAsync(ct);

        var decisionByGroupKey = await _db.Set<CustomerMergeDecision>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value)
            .Select(x => new { x.GroupKey, x.SuggestionUpdatedAtUtc })
            .ToListAsync(ct);

        var decisionMap = decisionByGroupKey
            .GroupBy(x => x.GroupKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.SuggestionUpdatedAtUtc).First().SuggestionUpdatedAtUtc);

        var pendingSuggestionKeys = new HashSet<string>(
            activeSuggestions
                .Where(s => !decisionMap.TryGetValue(s.GroupKey, out var decidedForUpdatedAt) || decidedForUpdatedAt != s.UpdatedAtUtc)
                .Select(s => s.GroupKey));

        // already merged source customers out
        var customerRows = await (
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId.Value
            join ml in _db.Set<CustomerMergeLink>().AsNoTracking().Where(x => x.TenantId == tenantId.Value)
                on c.Id equals ml.SourceCustomerId into mlj
            from ml in mlj.DefaultIfEmpty()
            where ml == null
            select new
            {
                CustomerId = c.Id.Value,
                c.FirstName,
                c.LastName
            })
            .ToListAsync(ct);

        var groups = customerRows
            .Select(x => new
            {
                Key = NormalizeName(x.FirstName, x.LastName),
                Row = x
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key)
            .Where(g => g.Count() >= 2)
            .Where(g => !pendingSuggestionKeys.Contains(g.Key))
            .OrderByDescending(g => g.Count())
            .Take(take)
            .ToList();

        var allCustomerIds = groups
            .SelectMany(g => g.Select(x => x.Row.CustomerId))
            .Distinct()
            .ToList();

        var providerMap = await BuildProviderMapAsync(tenantId.Value, allCustomerIds, ct);

        var items = groups
            .Select(g => new
            {
                groupKey = g.Key,
                normalizedName = g.Key,
                count = g.Count(),
                customers = g
                    .Select(x => new
                    {
                        customerId = x.Row.CustomerId,
                        firstName = x.Row.FirstName,
                        lastName = x.Row.LastName,
                        providers = providerMap.TryGetValue(x.Row.CustomerId, out var providers) ? providers : Array.Empty<string>()
                    })
                    .ToList()
            })
            .ToList();

        return Ok(new { items });
    }

    [HttpGet("suggestions/details")]
    public async Task<IActionResult> ListSuggestionDetails([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest("Missing tenant context");

        if (take <= 0) take = 50;
        if (take > 200) take = 200;

        var nowUtc = DateTimeOffset.UtcNow;

        var suggestions = await _db.Set<CustomerMergeSuggestion>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value.Value && x.ExpiresAtUtc > nowUtc)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(take)
            .Select(x => new
            {
                x.GroupKey,
                x.PayloadJson,
                x.Confidence,
                x.NormalizedName,
                x.Rationale,
                x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        var decisions = await _db.Set<CustomerMergeDecision>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value)
            .Select(x => new
            {
                x.GroupKey,
                x.Status,
                x.CanonicalCustomerId,
                x.SuggestionUpdatedAtUtc,
                x.DecidedAtUtc
            })
            .ToListAsync(ct);

        var decisionMap = decisions
            .GroupBy(x => x.GroupKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DecidedAtUtc).First());

        var undecidedSuggestions = suggestions
            .Where(s => !decisionMap.TryGetValue(s.GroupKey, out var d) || d.SuggestionUpdatedAtUtc != s.UpdatedAtUtc)
            .ToList();

        // SelectMany inference sorunu çıkarmasın diye düz döngü
        var allCandidateIds = new List<Guid>();
        foreach (var s in undecidedSuggestions)
        {
            var grp = DeserializeSuggestionGroupOrNull(s.PayloadJson);
            if (grp is null) continue;

            foreach (var c in grp.Candidates)
                allCandidateIds.Add(c.CustomerId);
        }

        var distinctCandidateIds = allCandidateIds.Distinct().ToList();
        var providerMap = await BuildProviderMapAsync(tenantId.Value, distinctCandidateIds, ct);

        var items = undecidedSuggestions
            .Select(s =>
            {
                var grp = DeserializeSuggestionGroupOrNull(s.PayloadJson);
                if (grp is null)
                {
                    return new
                    {
                        groupKey = s.GroupKey,
                        confidence = (double)s.Confidence,
                        normalizedName = s.NormalizedName,
                        rationale = s.Rationale,
                        candidates = Array.Empty<object>()
                    };
                }

                var candidates = grp.Candidates
                    .Select(c => (object)new
                    {
                        customerId = c.CustomerId,
                        firstName = c.FirstName,
                        lastName = c.LastName,
                        providers = providerMap.TryGetValue(c.CustomerId, out var providers)
                            ? providers
                            : Array.Empty<string>(),
                        channels = c.Channels,
                        shippingAddress = c.ShippingAddress,
                        billingAddress = c.BillingAddress
                    })
                    .ToArray();

                return new
                {
                    groupKey = grp.GroupKey,
                    confidence = grp.Confidence,
                    normalizedName = grp.NormalizedName,
                    rationale = grp.Rationale,
                    candidates
                };
            })
            .ToList();

        return Ok(new { items });
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> ListSuggestions([FromQuery] int take = 20, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest("Missing tenant context");

        var items = await _suggestions.ListLatestAsync(tenantId.Value, take, ct);
        return Ok(new { items });
    }

    [HttpGet("suggestions/{groupKey}")]
    public async Task<IActionResult> GetSuggestion(string groupKey, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest("Missing tenant context");

        var item = await _suggestions.GetByGroupKeyAsync(tenantId.Value, groupKey, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("suggestions/{groupKey}/approve")]
    public async Task<IActionResult> ApproveSuggestion(string groupKey, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest("Missing tenant context");

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Suggestion'ı transaction dışında okumak istesen de olur ama biz tek retriable unit yapıyoruz
            var suggestion = await _db.Set<CustomerMergeSuggestion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value.Value && x.GroupKey == groupKey, ct);

            if (suggestion is null)
                return (IActionResult)NotFound();

            var group = DeserializeSuggestionGroupOrNull(suggestion.PayloadJson);
            if (group is null)
                return (IActionResult)Problem("Suggestion payload could not be parsed.");

            // Guid -> CustomerId
            var candidateIds = group.Candidates
                .Select(c => new CustomerId(c.CustomerId))
                .Distinct()
                .ToList();

            if (candidateIds.Count < 2)
                return (IActionResult)BadRequest("Suggestion must contain at least 2 candidates.");

            var existingLinks = await _db.Set<CustomerMergeLink>()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId.Value)
                .Select(x => new { x.SourceCustomerId, x.CanonicalCustomerId })
                .ToListAsync(ct);

            var linkMap = existingLinks.ToDictionary(x => x.SourceCustomerId, x => x.CanonicalCustomerId);

            CustomerId ResolveRoot(CustomerId id)
            {
                var current = id;
                var visited = new HashSet<CustomerId>();

                while (linkMap.TryGetValue(current, out var next))
                {
                    if (!visited.Add(current))
                        break;
                    current = next;
                }

                return current;
            }

            var roots = candidateIds.Select(ResolveRoot).Distinct().ToList();

            var allCustomerIds = new HashSet<CustomerId>(candidateIds);
            foreach (var root in roots)
                allCustomerIds.Add(root);

            foreach (var src in linkMap.Keys)
            {
                var root = ResolveRoot(src);
                if (roots.Contains(root))
                    allCustomerIds.Add(src);
            }

            // ✅ EF translate fix: Contains on strong id
            var allTypedIds = allCustomerIds.ToList();
            var orderCounts = await _db.Orders.AsNoTracking()
                .Where(o => o.TenantId == tenantId.Value && allTypedIds.Contains(o.CustomerId))
                .GroupBy(o => o.CustomerId)
                .Select(g => new { CustomerId = g.Key, Cnt = g.Count() })
                .ToListAsync(ct);

            var orderCountMap = orderCounts.ToDictionary(x => x.CustomerId, x => x.Cnt);

            // ✅ EF translate fix: Contains on strong id
            var rootIds = roots.ToList();
            var customerMeta = await _db.Customers.AsNoTracking()
                .Where(c => c.TenantId == tenantId.Value && rootIds.Contains(c.Id))
                .Select(c => new { c.Id, c.FirstSeenAtUtc, c.LastSeenAtUtc })
                .ToListAsync(ct);

            var metaMap = customerMeta.ToDictionary(x => x.Id, x => x);

            int GroupOrderCount(CustomerId root)
            {
                var sum = 0;
                foreach (var id in allCustomerIds)
                {
                    if (ResolveRoot(id) != root) continue;
                    if (orderCountMap.TryGetValue(id, out var cnt))
                        sum += cnt;
                }
                return sum;
            }

            var canonical = roots
                .OrderByDescending(GroupOrderCount)
                .ThenByDescending(r => metaMap.TryGetValue(r, out var m) ? m.LastSeenAtUtc : DateTimeOffset.MinValue)
                .ThenBy(r => metaMap.TryGetValue(r, out var m) ? m.FirstSeenAtUtc : DateTimeOffset.MaxValue)
                .ThenBy(r => r.Value)
                .First();

            var nowUtc = DateTimeOffset.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var decision = await _db.Set<CustomerMergeDecision>()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.GroupKey == groupKey, ct);

            if (decision is null)
            {
                decision = new CustomerMergeDecision(
                    id: Guid.NewGuid(),
                    tenantId: tenantId.Value,
                    groupKey: groupKey,
                    status: CustomerMergeDecisionStatus.Approved,
                    canonicalCustomerId: canonical.Value,
                    suggestionUpdatedAtUtc: suggestion.UpdatedAtUtc,
                    nowUtc: nowUtc);
                _db.Add(decision);
            }
            else
            {
                // Eğer MarkApproved(CustomerId ...) ise canonical.Value yerine canonical ver
                decision.MarkApproved(canonical.Value, suggestion.UpdatedAtUtc, nowUtc);
            }

            var toUpsert = allCustomerIds.Where(x => x != canonical).ToList();

            // ✅ EF translate fix: Contains on strong id (SourceCustomerId)
            var existingTrackedLinks = await _db.Set<CustomerMergeLink>()
                .Where(x => x.TenantId == tenantId.Value && toUpsert.Contains(x.SourceCustomerId))
                .ToListAsync(ct);

            var existingBySource = existingTrackedLinks.ToDictionary(x => x.SourceCustomerId, x => x);

            foreach (var sourceId in toUpsert)
            {
                if (existingBySource.TryGetValue(sourceId, out var link))
                {
                    link.PointTo(canonical, groupKey, nowUtc);
                    continue;
                }

                _db.Add(new CustomerMergeLink(tenantId.Value, sourceId, canonical, groupKey, nowUtc));
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return (IActionResult)Ok(new
            {
                groupKey,
                canonicalCustomerId = canonical.Value,
                mergedCustomerIds = allCustomerIds.Select(x => x.Value).ToList()
            });
        });
    }

    [HttpPost("suggestions/{groupKey}/reject")]
    public async Task<IActionResult> RejectSuggestion(string groupKey, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest("Missing tenant context");

        var suggestion = await _db.Set<CustomerMergeSuggestion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value.Value && x.GroupKey == groupKey, ct);

        if (suggestion is null)
            return NotFound();

        var nowUtc = DateTimeOffset.UtcNow;

        var decision = await _db.Set<CustomerMergeDecision>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.GroupKey == groupKey, ct);

        if (decision is null)
        {
            decision = new CustomerMergeDecision(
                id: Guid.NewGuid(),
                tenantId: tenantId.Value,
                groupKey: groupKey,
                status: CustomerMergeDecisionStatus.Rejected,
                canonicalCustomerId: null,
                suggestionUpdatedAtUtc: suggestion.UpdatedAtUtc,
                nowUtc: nowUtc);
            _db.Add(decision);
        }
        else
        {
            decision.MarkRejected(suggestion.UpdatedAtUtc, nowUtc);
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { groupKey, status = "rejected" });
    }

    private static CustomerDuplicateGroupDto? DeserializeSuggestionGroupOrNull(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<CustomerDuplicateGroupDto>(payloadJson, SuggestionPayloadJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<Guid, string[]>> BuildProviderMapAsync(
        TenantId tenantId,
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken ct)
    {
        if (customerIds.Count == 0)
            return new Dictionary<Guid, string[]>();

        var idList = customerIds.Distinct().ToList();
        var typedIds = idList.Select(x => new CustomerId(x)).ToList();

        var identityProviders = await _db.Customers.AsNoTracking()
            .Where(c => c.TenantId == tenantId && typedIds.Contains(c.Id)) // ✅ Contains on strong id
            .SelectMany(c => c.Identities
                .Where(i => i.Type == IdentityType.ProviderCustomerId && i.SourceProvider.HasValue)
                .Select(i => new
                {
                    CustomerId = c.Id.Value,
                    Provider = i.SourceProvider!.Value.ToString().ToLowerInvariant()
                }))
            .ToListAsync(ct);

        var providers = identityProviders
            .GroupBy(x => x.CustomerId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Provider).Distinct().OrderBy(x => x).ToArray());

        var missingGuids = idList.Where(id => !providers.ContainsKey(id)).ToList();
        if (missingGuids.Count == 0)
            return providers;

        var missingTyped = missingGuids.Select(x => new CustomerId(x)).ToList();

        var orderProviders = await _db.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId && missingTyped.Contains(o.CustomerId)) // ✅ Contains on strong id
            .Select(o => new
            {
                CustomerId = o.CustomerId.Value,
                Provider = o.Channel.ToString().ToLowerInvariant()
            })
            .ToListAsync(ct);

        foreach (var grp in orderProviders.GroupBy(x => x.CustomerId))
        {
            providers[grp.Key] = grp.Select(x => x.Provider).Distinct().OrderBy(x => x).ToArray();
        }

        return providers;
    }

    private static string NormalizeName(string? first, string? last)
    {
        var f = NormalizeToken(first);
        var l = NormalizeToken(last);

        if (string.IsNullOrWhiteSpace(f) && string.IsNullOrWhiteSpace(l))
            return string.Empty;

        return (f + " " + l).Trim();
    }

    private static string NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var t = token.Trim().ToLowerInvariant();
        while (t.Contains("  ", StringComparison.Ordinal))
            t = t.Replace("  ", " ", StringComparison.Ordinal);

        return t;
    }
}
