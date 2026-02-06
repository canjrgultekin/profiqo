using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Application.Abstractions.Tenancy;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/whatsapp/rules")]
[Authorize]
public sealed class WhatsappRulesController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IWhatsappRuleRepository _repo;

    public WhatsappRulesController(ITenantContext tenant, IWhatsappRuleRepository repo)
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

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] WhatsappRuleDto dto, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var normalized = dto with { TenantId = tid };
        var id = await _repo.UpsertAsync(normalized, ct);
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
