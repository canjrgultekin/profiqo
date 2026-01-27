using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Persistence;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class OrdersController : ControllerBase
{
    private readonly ProfiqoDbContext _db;
    private readonly ITenantContext _tenant;

    public OrdersController(ProfiqoDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? q = null,
        [FromQuery] Guid? customerId = null,
        CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        var query = _db.Orders.AsNoTracking();

        if (customerId.HasValue && customerId.Value != Guid.Empty)
            query = query.Where(o => o.CustomerId.Value == customerId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(o =>
                (o.ProviderOrderId != null && EF.Functions.ILike(o.ProviderOrderId, $"%{s}%")));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.PlacedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                orderId = o.Id.Value,
                tenantId = o.TenantId.Value,
                customerId = o.CustomerId.Value,

                channel = o.Channel.ToString(),
                status = o.Status.ToString(),
                providerOrderId = o.ProviderOrderId,

                placedAtUtc = o.PlacedAtUtc,
                completedAtUtc = o.CompletedAtUtc,

                totalAmount = o.TotalAmount.Amount,
                totalCurrency = o.TotalAmount.Currency.Value,

                netProfit = o.NetProfit.Amount,
                netProfitCurrency = o.NetProfit.Currency.Value,

                lineCount = o.Lines.Count
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
