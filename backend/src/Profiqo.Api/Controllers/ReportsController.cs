using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class ReportsController : ControllerBase
{
    private readonly ProfiqoDbContext _db;
    private readonly ITenantContext _tenant;

    public ReportsController(ProfiqoDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var now = DateTimeOffset.UtcNow;
        var since30 = now.AddDays(-30);

        var links = _db.Set<CustomerMergeLink>().AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value);

        var customerCanonicals =
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId.Value
            join ml in links on c.Id equals ml.SourceCustomerId into mlj
            from ml in mlj.DefaultIfEmpty()
            select new
            {
                canonicalCustomerId = ml != null ? ml.CanonicalCustomerId : c.Id,
                c.LastSeenAtUtc
            };

        var totalCustomers = await customerCanonicals
            .Select(x => x.canonicalCustomerId)
            .Distinct()
            .CountAsync(ct);

        var activeCustomers30 = await customerCanonicals
            .GroupBy(x => x.canonicalCustomerId)
            .Select(g => new { lastSeenAtUtc = g.Max(x => x.LastSeenAtUtc) })
            .CountAsync(x => x.lastSeenAtUtc >= since30, ct);

        var totalOrders30 = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId.Value && x.PlacedAtUtc >= since30, ct);

        var grossRevenue30 = await _db.Orders.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.PlacedAtUtc >= since30)
            .Select(x => x.TotalAmount.Amount)
            .SumAsync(ct);

        var netProfit30 = await _db.Orders.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.PlacedAtUtc >= since30)
            .Select(x => x.NetProfit.Amount)
            .SumAsync(ct);

        return Ok(new
        {
            windowDays = 30,
            totalCustomers,
            activeCustomers30,
            totalOrders30,
            grossRevenue30,
            netProfit30,
            currency = "TRY"
        });
    }

    [HttpGet("channel-breakdown")]
    public async Task<IActionResult> ChannelBreakdown(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var since30 = DateTimeOffset.UtcNow.AddDays(-30);

        var rows = await _db.Orders.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.PlacedAtUtc >= since30)
            .GroupBy(x => x.Channel)
            .Select(g => new
            {
                channel = g.Key.ToString(),
                orders = g.Count(),
                gross = g.Sum(x => x.TotalAmount.Amount),
                profit = g.Sum(x => x.NetProfit.Amount)
            })
            .OrderByDescending(x => x.orders)
            .ToListAsync(ct);

        return Ok(new { windowDays = 30, currency = "TRY", items = rows });
    }
}
