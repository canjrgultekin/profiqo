using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Integrations.Ikas.Commands.ConnectIkas;
using Profiqo.Application.Integrations.Ikas.Commands.StartIkasSync;
using Profiqo.Application.Integrations.Ikas.Commands.TestIkas;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/integrations/ikas")]
[Authorize]
public sealed class IkasIntegrationController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IIntegrationJobRepository _jobs;

    public IkasIntegrationController(ISender sender, IIntegrationJobRepository jobs)
    {
        _sender = sender;
        _jobs = jobs;
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
