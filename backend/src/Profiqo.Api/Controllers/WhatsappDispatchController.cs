using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Application.Abstractions.Tenancy;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/whatsapp/dispatch")]
[Authorize]
public sealed class WhatsappDispatchController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IWhatsappDispatchRepository _dispatch;

    public WhatsappDispatchController(ITenantContext tenant, IWhatsappDispatchRepository dispatch)
    {
        _tenant = tenant;
        _dispatch = dispatch;
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int take = 100, CancellationToken ct = default)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var items = await _dispatch.ListRecentAsync(tid, take, ct);
        return Ok(new { items });
    }

    public sealed record ManualEnqueueRequest(
        Guid JobId,
        Guid RuleId,
        Guid CustomerId,
        string ToE164,
        short MessageNo,
        Guid TemplateId,
        DateTimeOffset PlannedAtUtc,
        string PayloadJson);

    [HttpPost("manual-enqueue")]
    public async Task<IActionResult> ManualEnqueue([FromBody] ManualEnqueueRequest req, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).Date);
        var id = await _dispatch.EnqueueManualAsync(tid, req.JobId, req.RuleId, req.CustomerId, req.ToE164, req.MessageNo, req.TemplateId, req.PlannedAtUtc, localDate, EnsureJson(req.PayloadJson), ct);

        return Ok(new { id });
    }

    private static string EnsureJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetRawText();
    }
}
