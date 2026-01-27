using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
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
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        var query = _db.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId.Value);

        if (customerId.HasValue && customerId.Value != Guid.Empty)
        {
            var cid = new CustomerId(customerId.Value);
            query = query.Where(o => o.CustomerId == cid); // ✅ FIX
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(o => o.ProviderOrderId != null && EF.Functions.ILike(o.ProviderOrderId, $"%{s}%"));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.PlacedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                orderId = o.Id.Value,
                customerId = o.CustomerId.Value,
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

        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid orderId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var oid = new OrderId(orderId);

        var o = await _db.Orders.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.Id == oid) // ✅ FIX
            .Select(x => new
            {
                orderId = x.Id.Value,
                customerId = x.CustomerId.Value,
                providerOrderId = x.ProviderOrderId,
                channel = x.Channel.ToString(),
                status = x.Status.ToString(),
                placedAtUtc = x.PlacedAtUtc,
                completedAtUtc = x.CompletedAtUtc,
                totalAmount = x.TotalAmount.Amount,
                totalCurrency = x.TotalAmount.Currency.Value,
                netProfit = x.NetProfit.Amount,
                netProfitCurrency = x.NetProfit.Currency.Value,
                costBreakdownJson = x.CostBreakdownJson,
                lines = x.Lines.Select(l => new
                {
                    sku = l.Sku,
                    productName = l.ProductName,
                    quantity = l.Quantity,
                    unitPrice = l.UnitPrice.Amount,
                    unitCurrency = l.UnitPrice.Currency.Value,
                    lineTotal = l.LineTotal.Amount,
                    lineTotalCurrency = l.LineTotal.Currency.Value
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (o is null) return NotFound(new { message = "Order not found." });
        return Ok(o);
    }
}
