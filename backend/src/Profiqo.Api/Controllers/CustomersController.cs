using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class CustomersController : ControllerBase
{
    private readonly ProfiqoDbContext _db;
    private readonly ITenantContext _tenant;

    public CustomersController(ProfiqoDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private async Task<CustomerId> ResolveCanonicalAsync(TenantId tenantId, CustomerId input, CancellationToken ct)
    {
        // Chain ihtimaline karşı limitli takip
        var current = input;

        for (var i = 0; i < 8; i++)
        {
            var link = await _db.Set<CustomerMergeLink>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SourceCustomerId == current, ct);

            if (link is null) break;

            if (link.CanonicalCustomerId.Equals(current))
                break;

            current = link.CanonicalCustomerId;
        }

        return current;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        var links = _db.Set<CustomerMergeLink>().AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value);

        var baseQuery =
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId.Value
            join ml in links on c.Id equals ml.SourceCustomerId into mlj
            from ml in mlj.DefaultIfEmpty()
            select new
            {
                Customer = c,
                CanonicalCustomerId = ml != null ? ml.CanonicalCustomerId : c.Id
            };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            baseQuery = baseQuery.Where(x =>
                (x.Customer.FirstName != null && EF.Functions.ILike(x.Customer.FirstName, $"%{s}%")) ||
                (x.Customer.LastName != null && EF.Functions.ILike(x.Customer.LastName, $"%{s}%")));
        }

        var grouped = baseQuery
            .GroupBy(x => x.CanonicalCustomerId)
            .Select(g => new
            {
                CanonicalCustomerId = g.Key,
                FirstSeenAtUtc = g.Min(x => x.Customer.FirstSeenAtUtc),
                LastSeenAtUtc = g.Max(x => x.Customer.LastSeenAtUtc)
            });

        var total = await grouped.CountAsync(ct);

        var pageRows = await grouped
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var canonicalIds = pageRows.Select(x => x.CanonicalCustomerId).ToList();

        var canonicalCustomers = await _db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && canonicalIds.Contains(x.Id))
            .Select(x => new
            {
                customerId = x.Id.Value,
                firstName = x.FirstName,
                lastName = x.LastName,
                rfmSegment = x.Rfm != null ? x.Rfm.Segment.ToString() : null,
                churnRisk = x.AiScores != null ? x.AiScores.ChurnRiskScore : (int?)null,
                ltv12mProfit = x.AiScores != null ? x.AiScores.Ltv12mProfit : (decimal?)null
            })
            .ToListAsync(ct);

        var map = canonicalCustomers.ToDictionary(x => x.customerId, x => x);

        var items = pageRows.Select(r =>
        {
            map.TryGetValue(r.CanonicalCustomerId.Value, out var c);

            return new
            {
                customerId = r.CanonicalCustomerId.Value,
                firstName = c?.firstName,
                lastName = c?.lastName,
                firstSeenAtUtc = r.FirstSeenAtUtc,
                lastSeenAtUtc = r.LastSeenAtUtc,
                rfmSegment = c?.rfmSegment,
                churnRisk = c?.churnRisk,
                ltv12mProfit = c?.ltv12mProfit
            };
        });

        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid customerId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var input = new CustomerId(customerId);
        var canonical = await ResolveCanonicalAsync(tenantId.Value, input, ct);

        var memberIds = await _db.Set<CustomerMergeLink>().AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.CanonicalCustomerId == canonical)
            .Select(x => x.SourceCustomerId)
            .ToListAsync(ct);

        // canonical kendisi de member
        memberIds.Add(canonical);

        var rows = await _db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && memberIds.Contains(x.Id))
            .Select(x => new
            {
                customerId = x.Id.Value,
                firstName = x.FirstName,
                lastName = x.LastName,
                firstSeenAtUtc = x.FirstSeenAtUtc,
                lastSeenAtUtc = x.LastSeenAtUtc,
                createdAtUtc = x.CreatedAtUtc,
                updatedAtUtc = x.UpdatedAtUtc,
                rfm = x.Rfm == null ? null : new
                {
                    r = x.Rfm.RecencyScore,
                    f = x.Rfm.FrequencyScore,
                    m = x.Rfm.MonetaryScore,
                    segment = x.Rfm.Segment.ToString(),
                    computedAtUtc = x.Rfm.ComputedAtUtc
                },
                ai = x.AiScores == null ? null : new
                {
                    ltv12mProfit = x.AiScores.Ltv12mProfit,
                    churnRiskScore = x.AiScores.ChurnRiskScore,
                    nextPurchaseAtUtc = x.AiScores.NextPurchaseAtUtc,
                    discountSensitivityScore = x.AiScores.DiscountSensitivityScore,
                    computedAtUtc = x.AiScores.ComputedAtUtc
                },
                identities = x.Identities
                    .OrderByDescending(i => i.LastSeenAtUtc)
                    .Select(i => new
                    {
                        type = i.Type.ToString(),
                        valueHash = i.ValueHash.Value,
                        sourceProvider = i.SourceProvider.HasValue ? i.SourceProvider.Value.ToString() : null,
                        sourceExternalId = i.SourceExternalId,
                        firstSeenAtUtc = i.FirstSeenAtUtc,
                        lastSeenAtUtc = i.LastSeenAtUtc
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        var canonicalRow = rows.FirstOrDefault(x => x.customerId == canonical.Value);
        if (canonicalRow is null) return NotFound(new { message = "Customer not found." });

        var firstSeen = rows.Min(x => x.firstSeenAtUtc);
        var lastSeen = rows.Max(x => x.lastSeenAtUtc);

        var mergedIdentities = rows
            .SelectMany(x => x.identities)
            .GroupBy(i => new { i.type, i.valueHash, i.sourceProvider, i.sourceExternalId })
            .Select(g => g.OrderByDescending(x => x.lastSeenAtUtc).First())
            .OrderByDescending(x => x.lastSeenAtUtc)
            .ToList();

        return Ok(new
        {
            customerId = canonical.Value,
            canonicalCustomerId = canonical.Value,
            mergedFromCustomerIds = rows.Select(x => x.customerId).Distinct().OrderBy(x => x).ToArray(),

            firstName = canonicalRow.firstName,
            lastName = canonicalRow.lastName,

            firstSeenAtUtc = firstSeen,
            lastSeenAtUtc = lastSeen,

            createdAtUtc = canonicalRow.createdAtUtc,
            updatedAtUtc = canonicalRow.updatedAtUtc,

            rfm = canonicalRow.rfm,
            ai = canonicalRow.ai,
            identities = mergedIdentities
        });
    }

    [HttpGet("{customerId:guid}/orders")]
    public async Task<IActionResult> Orders([FromRoute] Guid customerId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var input = new CustomerId(customerId);
        var canonical = await ResolveCanonicalAsync(tenantId.Value, input, ct);

        // canonical + tüm source’lar
        var memberIds = await _db.Set<CustomerMergeLink>().AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.CanonicalCustomerId == canonical)
            .Select(x => x.SourceCustomerId)
            .ToListAsync(ct);

        memberIds.Add(canonical);

        // canonical müşteri var mı (source id ile gelmişse canonical’a çözdük, o yüzden kontrol bu)
        var canonicalExists = await _db.Customers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId.Value && x.Id == canonical, ct);

        if (!canonicalExists) return NotFound(new { message = "Customer not found." });

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId.Value && memberIds.Contains(o.CustomerId))
            .OrderByDescending(o => o.PlacedAtUtc)
            .Select(o => new
            {
                orderId = o.Id.Value,
                providerOrderId = o.ProviderOrderId,
                channel = o.Channel.ToString(),
                status = o.Status.ToString(),
                placedAtUtc = o.PlacedAtUtc,

                totalAmount = o.TotalAmount.Amount,
                totalCurrency = o.TotalAmount.Currency.Value,

                netProfit = o.NetProfit.Amount,
                netProfitCurrency = o.NetProfit.Currency.Value,

                lineCount = o.Lines.Count,

                // debug/trace: hangi source customer’dan gelmiş
                sourceCustomerId = o.CustomerId.Value,
                canonicalCustomerId = canonical.Value
            })
            .ToListAsync(ct);

        return Ok(new { items = orders });
    }
}
