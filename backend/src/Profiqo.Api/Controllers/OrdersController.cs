// Path: backend/src/Profiqo.Api/Controllers/OrdersController.cs
using System.Text.Json;

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
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        var q = _db.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId.Value)
            .OrderByDescending(o => o.PlacedAtUtc);

        var total = await q.CountAsync(ct);

        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                orderId = o.Id.Value,
                providerOrderId = o.ProviderOrderId,
                channel = o.Channel.ToString(),
                status = o.Status.ToString(),
                placedAtUtc = o.PlacedAtUtc,
                totalAmount = new { amount = o.TotalAmount.Amount, currency = o.TotalAmount.Currency.Value },

                shippingAddressJson = EF.Property<string?>(o, "ShippingAddressJson"),
                billingAddressJson = EF.Property<string?>(o, "BillingAddressJson")
            })
            .ToListAsync(ct);

        object ParseMini(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new { city = (string?)null, district = (string?)null, postalCode = (string?)null, country = (string?)null };

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? city = null;
                string? district = null;
                string? postalCode = null;
                string? country = null;

                // Trendyol normalized json
                if (root.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.String) city = c.GetString();
                if (root.TryGetProperty("district", out var d) && d.ValueKind == JsonValueKind.String) district = d.GetString();
                if (root.TryGetProperty("postalCode", out var p) && p.ValueKind == JsonValueKind.String) postalCode = p.GetString();
                if (root.TryGetProperty("country", out var co) && co.ValueKind == JsonValueKind.String) country = co.GetString();

                // Ikas raw json: city{name}, district{name}, country{code/name}
                if (city is null && root.TryGetProperty("city", out var ic) && ic.ValueKind == JsonValueKind.Object)
                    city = ic.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;

                if (district is null && root.TryGetProperty("district", out var id) && id.ValueKind == JsonValueKind.Object)
                    district = id.TryGetProperty("name", out var n2) && n2.ValueKind == JsonValueKind.String ? n2.GetString() : null;

                if (postalCode is null && root.TryGetProperty("postalCode", out var pc) && pc.ValueKind == JsonValueKind.String)
                    postalCode = pc.GetString();

                if (country is null && root.TryGetProperty("country", out var ico) && ico.ValueKind == JsonValueKind.Object)
                {
                    country =
                        ico.TryGetProperty("code", out var cc) && cc.ValueKind == JsonValueKind.String ? cc.GetString()
                        : ico.TryGetProperty("name", out var cn) && cn.ValueKind == JsonValueKind.String ? cn.GetString()
                        : null;
                }

                return new { city, district, postalCode, country };
            }
            catch
            {
                return new { city = (string?)null, district = (string?)null, postalCode = (string?)null, country = (string?)null };
            }
        }

        var mapped = items.Select(x => new
        {
            x.orderId,
            x.providerOrderId,
            x.channel,
            x.status,
            x.placedAtUtc,
            x.totalAmount,
            shipping = ParseMini(x.shippingAddressJson),
            billing = ParseMini(x.billingAddressJson)
        });

        return Ok(new { page, pageSize, total, items = mapped });
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid orderId, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var oid = new OrderId(orderId);

        var o = await _db.Orders.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.Id == oid) // ✅ FIX: no .Value in predicate
            .Select(x => new
            {
                orderId = x.Id.Value,
                providerOrderId = x.ProviderOrderId,
                channel = x.Channel.ToString(),
                status = x.Status.ToString(),
                placedAtUtc = x.PlacedAtUtc,
                totalAmount = new { amount = x.TotalAmount.Amount, currency = x.TotalAmount.Currency.Value },

                shippingAddressJson = EF.Property<string?>(x, "ShippingAddressJson"),
                billingAddressJson = EF.Property<string?>(x, "BillingAddressJson"),

                lines = x.Lines.Select(l => new
                {
                    sku = l.Sku,
                    productName = l.ProductName,
                    quantity = l.Quantity,
                    unitPrice = new { amount = l.UnitPrice.Amount, currency = l.UnitPrice.Currency.Value },
                    lineTotal = new { amount = l.LineTotal.Amount, currency = l.LineTotal.Currency.Value }
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (o is null) return NotFound(new { message = "Order not found." });

        object? ParseObj(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<object>(json);
            }
            catch
            {
                return new { raw = json };
            }
        }

        return Ok(new
        {
            o.orderId,
            o.providerOrderId,
            o.channel,
            o.status,
            o.placedAtUtc,
            o.totalAmount,
            shippingAddress = ParseObj(o.shippingAddressJson),
            billingAddress = ParseObj(o.billingAddressJson),
            lines = o.lines
        });
    }
}
