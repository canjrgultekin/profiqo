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

        // For now: only Ikas. Later Trendyol/Shopify etc will follow same pattern.
        var ikas = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Ikas, ct);
        if (ikas is not null)
        {
            var c1 = await _cursors.GetAsync(tenantId.Value, ikas.Id, "ikas.customers.updatedAt", ct);
            var c2 = await _cursors.GetAsync(tenantId.Value, ikas.Id, "ikas.orders.updatedAt", ct);

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
                    ordersUpdatedAtMs = c2
                },
                healthy = ikas.Status == ProviderConnectionStatus.Active
            });
        }

        return Ok(new { items = result });
    }
}
