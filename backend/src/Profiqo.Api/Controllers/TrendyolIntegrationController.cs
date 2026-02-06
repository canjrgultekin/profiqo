// Path: backend/src/Profiqo.Api/Controllers/TrendyolIntegrationController.cs
using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Integrations.Trendyol.Commands.ConnectTrendyol;
using Profiqo.Application.Integrations.Trendyol.Commands.StartTrendyolSync;
using Profiqo.Application.Integrations.Trendyol.Commands.TestTrendyol;
using Profiqo.Domain.Integrations;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/integrations/trendyol")]
[Authorize(Policy = AuthorizationPolicies.IntegrationAccess)]
public sealed class TrendyolIntegrationController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;

    public TrendyolIntegrationController(ISender sender, ITenantContext tenant, IProviderConnectionRepository connections)
    {
        _sender = sender;
        _tenant = tenant;
        _connections = connections;
    }

    [HttpGet("connection")]
    public async Task<IActionResult> GetConnection(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Tenant context missing." });

        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Trendyol, ct);
        if (conn is null) return Ok(new { hasConnection = false });

        return Ok(new
        {
            hasConnection = true,
            connectionId = conn.Id.Value,
            status = conn.Status.ToString(),
            displayName = conn.DisplayName,
            sellerId = conn.ExternalAccountId
        });
    }

    public sealed record ConnectRequest(string DisplayName, string SellerId, string ApiKey, string ApiSecret, string? UserAgent);

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req, CancellationToken ct)
    {
        var id = await _sender.Send(new ConnectTrendyolCommand(req.DisplayName, req.SellerId, req.ApiKey, req.ApiSecret, req.UserAgent), ct);
        return Ok(new { connectionId = id });
    }

    public sealed record TestRequest(Guid ConnectionId);

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestRequest req, CancellationToken ct)
    {
        var ok = await _sender.Send(new TestTrendyolCommand(req.ConnectionId), ct);
        return Ok(new { ok });
    }

    public sealed record StartRequest(Guid ConnectionId, int? PageSize, int? MaxPages);

    [HttpPost("sync/start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        var result = await _sender.Send(new StartTrendyolSyncCommand(req.ConnectionId, req.PageSize, req.MaxPages), ct);
        return Accepted(new { batchId = result.BatchId, jobs = result.Jobs.Select(x => new { jobId = x.JobId, kind = x.Kind }).ToArray() });
    }
}
