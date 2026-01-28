using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/reports/abandoned-checkouts")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class AbandonedCheckoutsReportController : ControllerBase
{
    private readonly ProfiqoDbContext _db;
    private readonly ITenantContext _tenant;

    public AbandonedCheckoutsReportController(ProfiqoDbContext db, ITenantContext tenant)
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

        var query = _db.Set<AbandonedCheckout>().AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();

            query = query.Where(x =>
                (x.CustomerEmail != null && EF.Functions.ILike(x.CustomerEmail, $"%{s}%")) ||
                (x.CustomerPhone != null && EF.Functions.ILike(x.CustomerPhone, $"%{s}%")) ||
                EF.Functions.ILike(x.ExternalId, $"%{s}%"));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.LastActivityDateMs)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.Id,
                externalId = x.ExternalId,
                providerType = x.ProviderType,
                customerEmail = x.CustomerEmail,
                customerPhone = x.CustomerPhone,
                lastActivityDateMs = x.LastActivityDateMs,
                lastActivityAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(x.LastActivityDateMs),
                currencyCode = x.CurrencyCode,
                totalFinalPrice = x.TotalFinalPrice,
                status = x.Status,
                updatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }
}
