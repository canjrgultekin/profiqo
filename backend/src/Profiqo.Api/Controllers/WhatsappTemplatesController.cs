using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Application.Abstractions.Tenancy;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/whatsapp/templates")]
[Authorize]
public sealed class WhatsappTemplatesController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IWhatsappTemplateDraftRepository _repo;

    public WhatsappTemplatesController(ITenantContext tenant, IWhatsappTemplateDraftRepository repo)
    {
        _tenant = tenant;
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var items = await _repo.ListAsync(tid, ct);
        return Ok(new { items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var item = await _repo.GetAsync(tid, id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    public sealed record UpsertRequest(Guid? Id, string Name, string Language, string Category, string ComponentsJson);

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertRequest req, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var id = await _repo.UpsertAsync(tid, req.Id, req.Name, req.Language, req.Category, req.ComponentsJson, ct);
        return Ok(new { id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        await _repo.DeleteAsync(tid, id, ct);
        return NoContent();
    }
}
