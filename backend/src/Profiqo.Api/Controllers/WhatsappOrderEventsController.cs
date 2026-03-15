using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/whatsapp/order-events")]
[Authorize(Policy = AuthorizationPolicies.IntegrationAccess)]
public sealed class WhatsappOrderEventsController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly ProfiqoDbContext _db;

    public WhatsappOrderEventsController(ITenantContext tenant, ProfiqoDbContext db)
    {
        _tenant = tenant;
        _db = db;
    }

    public sealed record CreateRequest(string OrderId, Guid CustomerId, string ToE164);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        if (string.IsNullOrWhiteSpace(req.OrderId)) return BadRequest(new { message = "OrderId required." });
        if (req.CustomerId == Guid.Empty) return BadRequest(new { message = "CustomerId required." });
        if (string.IsNullOrWhiteSpace(req.ToE164)) return BadRequest(new { message = "ToE164 required." });

        var exists = await _db.Set<WhatsappOrderEventRow>()
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tid && x.OrderId == req.OrderId, ct);

        if (exists) return Conflict(new { message = "Order event already exists for this orderId." });

        var row = new WhatsappOrderEventRow
        {
            Id = Guid.NewGuid(),
            TenantId = tid,
            OrderId = req.OrderId.Trim(),
            CustomerId = req.CustomerId,
            ToE164 = req.ToE164.Trim(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ProcessedAtUtc = null
        };

        await _db.AddAsync(row, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = row.Id });
    }
}
