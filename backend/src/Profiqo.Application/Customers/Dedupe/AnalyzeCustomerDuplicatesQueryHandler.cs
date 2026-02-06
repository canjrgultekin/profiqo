// Path: backend/src/Profiqo.Application/Customers/Dedupe/AnalyzeCustomerDuplicatesQueryHandler.cs
using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Orders;

namespace Profiqo.Application.Customers.Dedupe;

internal sealed class AnalyzeCustomerDuplicatesQueryHandler : IRequestHandler<AnalyzeCustomerDuplicatesQuery, AnalyzeCustomerDuplicatesResultDto>
{
    private readonly ITenantContext _tenant;
    private readonly ICustomerDedupeAnalysisRepository _repo;
    private readonly ICustomerMergeSuggestionRepository _suggestions;
    private readonly FuzzyAddressSimilarityScorer _fuzzy;
    private readonly AiCustomerSimilarityScorer? _ai;

    public AnalyzeCustomerDuplicatesQueryHandler(
        ITenantContext tenant,
        ICustomerDedupeAnalysisRepository repo,
        ICustomerMergeSuggestionRepository suggestions,
        FuzzyAddressSimilarityScorer fuzzy,
        AiCustomerSimilarityScorer? ai = null)
    {
        _tenant = tenant;
        _repo = repo;
        _suggestions = suggestions;
        _fuzzy = fuzzy;
        _ai = ai;
    }

    public async Task<AnalyzeCustomerDuplicatesResultDto> Handle(AnalyzeCustomerDuplicatesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new UnauthorizedException("Tenant context missing.");

        var threshold = request.Threshold is null or < 0.5 or > 0.99 ? 0.88 : request.Threshold.Value;

        var customers = await _repo.GetCustomerRowsAsync(tenantId.Value, ct);
        var aggs = await _repo.GetOrderAggsAsync(tenantId.Value, ct);
        var addr = await _repo.GetLatestAddressPairsAsync(tenantId.Value, ct);

        var aggMap = aggs.GroupBy(x => x.CustomerId).ToDictionary(g => g.Key, g => g.ToList());

        var nameGroups = customers
            .Select(c => new
            {
                c.CustomerId,
                c.FirstName,
                c.LastName,
                Key = NormalizeName(c.FirstName, c.LastName)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key)
            .Where(g => g.Count() >= 2)
            .ToList();

        var resultGroups = new List<CustomerDuplicateGroupDto>();

        foreach (var g in nameGroups)
        {
            var candidates = g.Select(x =>
            {
                var channels = aggMap.TryGetValue(x.CustomerId, out var list)
                    ? list.Select(a => new CustomerChannelSummaryDto(
                        Channel: ((SalesChannel)a.Channel).ToString(),
                        OrdersCount: a.OrdersCount,
                        TotalAmount: a.TotalAmount,
                        Currency: a.Currency)).ToList()
                    : new List<CustomerChannelSummaryDto>();

                addr.TryGetValue(x.CustomerId, out var ap);

                return new CustomerDuplicateCandidateDto(
                    CustomerId: x.CustomerId,
                    FirstName: x.FirstName,
                    LastName: x.LastName,
                    Channels: channels,
                    ShippingAddress: ap?.Shipping,
                    BillingAddress: ap?.Billing);
            }).ToList();

            var bestScore = 0d;

            for (var i = 0; i < candidates.Count; i++)
            {
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var f = await _fuzzy.ScoreAsync(candidates[i], candidates[j], ct);
                    var a = 0d;

                    if (_ai is not null)
                        a = await _ai.ScoreAsync(candidates[i], candidates[j], ct);

                    var score = _ai is null ? f : Math.Min(1.0, f * 0.7 + a * 0.3);
                    if (score > bestScore) bestScore = score;
                }
            }

            if (bestScore < threshold)
                continue;

            resultGroups.Add(new CustomerDuplicateGroupDto(
                GroupKey: g.Key,
                Confidence: Math.Round(bestScore, 4),
                NormalizedName: g.Key,
                Candidates: candidates,
                Rationale: _ai is null
                    ? "Name exact match + address fuzzy similarity"
                    : "Name exact match + hybrid (fuzzy+AI) similarity"));
        }

        resultGroups = resultGroups.OrderByDescending(x => x.Confidence).ToList();

        // ✅ Persist suggestions (TTL 24h)
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(24);

        var upserts = resultGroups.Select(grp => new CustomerMergeSuggestionUpsert(
            GroupKey: grp.GroupKey,
            Confidence: grp.Confidence,
            NormalizedName: grp.NormalizedName,
            Rationale: grp.Rationale,
            PayloadJson: JsonSerializer.Serialize(grp)
        )).ToList();

        await _suggestions.UpsertBatchAsync(tenantId.Value, upserts, now, expires, ct);

        return new AnalyzeCustomerDuplicatesResultDto(resultGroups);
    }

    private static string NormalizeName(string? first, string? last)
        => NormalizeToken($"{first ?? ""} {last ?? ""}");

    private static string NormalizeToken(string s)
    {
        s = s.Trim().ToLowerInvariant();
        s = s.Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g').Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');

        var chars = s.Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)).ToArray();
        var t = new string(chars);
        while (t.Contains("  ", StringComparison.Ordinal)) t = t.Replace("  ", " ", StringComparison.Ordinal);
        return t.Trim();
    }
}
