using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Integrations;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize]
public sealed class IntegrationsController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IIntegrationCursorRepository _cursors;

    public IntegrationsController(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        IIntegrationCursorRepository cursors)
    {
        _tenant = tenant;
        _connections = connections;
        _cursors = cursors;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        var result = new List<object>();

        // ikas
        var ikas = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Ikas, ct);
        if (ikas is not null)
        {
            var c1 = await _cursors.GetAsync(tenantId.Value, ikas.Id, "ikas.customers.updatedAt", ct);
            var c2 = await _cursors.GetAsync(tenantId.Value, ikas.Id, "ikas.orders.updatedAt", ct);
            var c3 = await _cursors.GetAsync(tenantId.Value, ikas.Id, "ikas.abandoned.cursor.lastActivityDateMs", ct);

            result.Add(new
            {
                provider = "ikas",
                connectionId = ikas.Id.Value,
                status = ikas.Status.ToString(),
                displayName = ikas.DisplayName,
                externalAccountId = ikas.ExternalAccountId,
                cursors = new
                {
                    customersUpdatedAtMs = c1,
                    ordersUpdatedAtMs = c2,
                    abandonedLastActivityMs = c3
                },
                healthy = ikas.Status == ProviderConnectionStatus.Active
            });
        }

        // Trendyol
        var trendyol = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Trendyol, ct);
        if (trendyol is not null)
        {
            result.Add(new
            {
                provider = "trendyol",
                connectionId = trendyol.Id.Value,
                status = trendyol.Status.ToString(),
                displayName = trendyol.DisplayName,
                externalAccountId = trendyol.ExternalAccountId,
                cursors = (object?)null,
                healthy = trendyol.Status == ProviderConnectionStatus.Active
            });
        }

        // WhatsApp
        var whatsapp = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Whatsapp, ct);
        if (whatsapp is not null)
        {
            result.Add(new
            {
                provider = "whatsapp",
                connectionId = whatsapp.Id.Value,
                status = whatsapp.Status.ToString(),
                displayName = whatsapp.DisplayName,
                externalAccountId = whatsapp.ExternalAccountId,
                cursors = (object?)null,
                healthy = whatsapp.Status == ProviderConnectionStatus.Active
            });
        }

        // Pixel (Storefront Events)
        var pixel = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Pixel, ct);
        if (pixel is not null)
        {
            result.Add(new
            {
                provider = "pixel",
                connectionId = pixel.Id.Value,
                status = pixel.Status.ToString(),
                displayName = pixel.DisplayName,
                externalAccountId = pixel.ExternalAccountId,
                cursors = (object?)null,
                healthy = pixel.Status == ProviderConnectionStatus.Active
            });
        }

        return Ok(new { items = result });
    }
}
