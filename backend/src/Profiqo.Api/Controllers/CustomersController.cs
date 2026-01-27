using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Infrastructure.Persistence;

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

        var query = _db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(x =>
                (x.FirstName != null && EF.Functions.ILike(x.FirstName, $"%{s}%")) ||
                (x.LastName != null && EF.Functions.ILike(x.LastName, $"%{s}%")));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                customerId = x.Id.Value, // projection ok
                firstName = x.FirstName,
                lastName = x.LastName,
                firstSeenAtUtc = x.FirstSeenAtUtc,
                lastSeenAtUtc = x.LastSeenAtUtc,
                rfmSegment = x.Rfm != null ? x.Rfm.Segment.ToString() : null,
                churnRisk = x.AiScores != null ? x.AiScores.ChurnRiskScore : (int?)null,
                ltv12mProfit = x.AiScores != null ? x.AiScores.Ltv12mProfit : (decimal?)null
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid customerId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var cid = new CustomerId(customerId);

        var c = await _db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.Id == cid) // ✅ FIX
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
            .FirstOrDefaultAsync(ct);

        if (c is null) return NotFound(new { message = "Customer not found." });
        return Ok(c);
    }

    [HttpGet("{customerId:guid}/orders")]
    public async Task<IActionResult> Orders([FromRoute] Guid customerId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var cid = new CustomerId(customerId);

        var exists = await _db.Customers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId.Value && x.Id == cid, ct); // ✅ FIX

        if (!exists) return NotFound(new { message = "Customer not found." });

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId.Value && o.CustomerId == cid) // ✅ FIX
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
                lineCount = o.Lines.Count
            })
            .ToListAsync(ct);

        return Ok(new { items = orders });
    }
}
