using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
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
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        var query = _db.Customers.AsNoTracking();

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
                customerId = x.Id.Value,
                tenantId = x.TenantId.Value,
                firstName = x.FirstName,
                lastName = x.LastName,
                firstSeenAtUtc = x.FirstSeenAtUtc,
                lastSeenAtUtc = x.LastSeenAtUtc,
                rfmSegment = x.Rfm != null ? x.Rfm.Segment.ToString() : null,
                churnRisk = x.AiScores != null ? x.AiScores.ChurnRiskScore : (int?)null,
                ltv12mProfit = x.AiScores != null ? x.AiScores.Ltv12mProfit : (decimal?)null
            })
            .ToListAsync(ct);

        return Ok(new
        {
            page,
            pageSize,
            total,
            items
        });
    }
}
