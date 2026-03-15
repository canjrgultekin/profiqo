// Path: backend/src/Profiqo.Api/Controllers/HepsiburadaIntegrationController.cs
using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Integrations.Hepsiburada.Commands.ConnectHepsiburada;
using Profiqo.Application.Integrations.Hepsiburada.Commands.StartHepsiburadaSync;
using Profiqo.Application.Integrations.Hepsiburada.Commands.TestHepsiburada;
using Profiqo.Domain.Integrations;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/integrations/hepsiburada")]
[Authorize(Policy = AuthorizationPolicies.IntegrationAccess)]
public sealed class HepsiburadaIntegrationController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;

    public HepsiburadaIntegrationController(ISender sender, ITenantContext tenant, IProviderConnectionRepository connections)
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

        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Hepsiburada, ct);
        if (conn is null) return Ok(new { hasConnection = false });

        return Ok(new
        {
            hasConnection = true,
            connectionId = conn.Id.Value,
            status = conn.Status.ToString(),
            displayName = conn.DisplayName,
            merchantId = conn.ExternalAccountId
        });
    }

    public sealed record ConnectRequest(string DisplayName, string MerchantId, string Username, string Password);

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req, CancellationToken ct)
    {
        var id = await _sender.Send(new ConnectHepsiburadaCommand(req.DisplayName, req.MerchantId, req.Username, req.Password), ct);
        return Ok(new { connectionId = id });
    }

    public sealed record TestRequest(Guid ConnectionId);

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestRequest req, CancellationToken ct)
    {
        var ok = await _sender.Send(new TestHepsiburadaCommand(req.ConnectionId), ct);
        return Ok(new { ok });
    }

    public sealed record StartRequest(Guid ConnectionId, int? PageSize, int? MaxPages);

    [HttpPost("sync/start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        var result = await _sender.Send(new StartHepsiburadaSyncCommand(req.ConnectionId, req.PageSize, req.MaxPages), ct);
        return Accepted(new { batchId = result.BatchId, jobs = result.Jobs.Select(x => new { jobId = x.JobId, kind = x.Kind }).ToArray() });
    }
}