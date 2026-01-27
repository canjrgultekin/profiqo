using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Integrations.Ikas.Commands.ConnectIkas;
using Profiqo.Application.Integrations.Ikas.Commands.StartIkasSync;
using Profiqo.Application.Integrations.Ikas.Commands.TestIkas;
using Profiqo.Domain.Integrations;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/integrations/ikas")]
[Authorize(Policy = AuthorizationPolicies.IntegrationAccess)]

public sealed class IkasIntegrationController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IIntegrationJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;

    public IkasIntegrationController(
        ISender sender,
        IIntegrationJobRepository jobs,
        ITenantContext tenant,
        IProviderConnectionRepository connections)
    {
        _sender = sender;
        _jobs = jobs;
        _tenant = tenant;
        _connections = connections;
    }

    // ✅ New: return existing ikas connection (token never returned)
    [HttpGet("connection")]
    public async Task<IActionResult> GetConnection(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Ikas, ct);
        if (conn is null)
            return Ok(new { hasConnection = false });

        return Ok(new
        {
            hasConnection = true,
            connectionId = conn.Id.Value,
            providerType = conn.ProviderType.ToString(),
            status = conn.Status.ToString(),
            displayName = conn.DisplayName,
            externalAccountId = conn.ExternalAccountId,
            accessTokenExpiresAtUtc = conn.AccessTokenExpiresAtUtc
        });
    }

    public sealed record ConnectRequest(string StoreLabel, string? StoreDomain, string AccessToken);

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req, CancellationToken ct)
    {
        var id = await _sender.Send(new ConnectIkasCommand(req.StoreLabel, req.StoreDomain, req.AccessToken), ct);
        return Ok(new { connectionId = id });
    }

    public sealed record TestRequest(Guid ConnectionId);

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestRequest req, CancellationToken ct)
    {
        var meId = await _sender.Send(new TestIkasCommand(req.ConnectionId), ct);
        return Ok(new { ok = true, meId });
    }

    public sealed record StartRequest(Guid ConnectionId, string Scope, int? PageSize, int? MaxPages);

    [HttpPost("sync/start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        var scope = (req.Scope ?? "both").Trim().ToLowerInvariant();

        var s = scope switch
        {
            "customers" => IkasSyncScope.Customers,
            "orders" => IkasSyncScope.Orders,
            _ => IkasSyncScope.Both
        };

        var result = await _sender.Send(new StartIkasSyncCommand(req.ConnectionId, s, req.PageSize, req.MaxPages), ct);
        return Accepted(result);
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob([FromRoute] Guid jobId, CancellationToken ct)
    {
        var j = await _jobs.GetAsync(jobId, ct);
        if (j is null) return NotFound(new { message = "Job not found." });
        return Ok(j);
    }

    [HttpGet("jobs/batch/{batchId:guid}")]
    public async Task<IActionResult> GetBatch([FromRoute] Guid batchId, CancellationToken ct)
    {
        var list = await _jobs.ListByBatchAsync(batchId, ct);
        return Ok(new { batchId, jobs = list });
    }
}
