using System;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Application.Abstractions.Tenancy;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/whatsapp/jobs")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class WhatsappJobsController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IWhatsappJobRepository _repo;

    private readonly IWhatsappRuleRepository _rules;
    private readonly IWhatsappDispatchRepository _dispatch;

    public WhatsappJobsController(ITenantContext tenant, IWhatsappJobRepository repo, IWhatsappRuleRepository rules, IWhatsappDispatchRepository dispatch)
    {
        _tenant = tenant;
        _repo = repo;
        _rules = rules;
        _dispatch = dispatch;
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
    public async Task<IActionResult> Upsert([FromBody] WhatsappJobDto dto, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var normalized = dto with { TenantId = tid };
        var id = await _repo.UpsertAsync(normalized, ct);
        return Ok(new { id });
    }

    public sealed class SetActiveRequest { public bool IsActive { get; set; } }

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest req, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        await _repo.SetActiveAsync(tid, id, req.IsActive, ct);
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        await _repo.DeleteAsync(tid, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/run-now")]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken ct)
    {
        var tid = _tenant.CurrentTenantId?.Value ?? Guid.Empty;
        if (tid == Guid.Empty) return BadRequest(new { message = "X-Tenant-Id required." });

        var job = await _repo.GetAsync(tid, id, ct);
        if (job is null) return NotFound(new { message = "Job not found." });

        var rule = await _rules.GetAsync(tid, job.RuleId, ct);
        if (rule is null) return BadRequest(new { message = "Rule not found for job." });

        // targets parse
        List<(Guid customerId, string to)> targets;
        try
        {
            using var doc = JsonDocument.Parse(job.TargetsJson);
            targets = doc.RootElement.EnumerateArray()
                .Select(x => (
                    customerId: Guid.Parse(x.GetProperty("customerId").GetString() ?? Guid.Empty.ToString()),
                    to: x.GetProperty("toE164").GetString() ?? ""))
                .Where(x => x.customerId != Guid.Empty && !string.IsNullOrWhiteSpace(x.to))
                .ToList();
        }
        catch
        {
            return BadRequest(new { message = "TargetsJson invalid." });
        }

        var tz = TimeZones.Istanbul;
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        var nowUtc = DateTimeOffset.UtcNow;
        var enq = 0;

        foreach (var t in targets)
        {
            var payload1 = JsonSerializer.Serialize(new { kind = "run_now", messageNo = 1, templateId = job.Template1Id });
            var id1 = await _dispatch.TryEnqueueUniqueAsync(tid, job.Id, job.RuleId, t.customerId, t.to, 1, job.Template1Id, nowUtc, localDate, payload1, ct);
            if (id1.HasValue) enq++;

            if (rule.DailyLimit >= 2 && job.Template2Id.HasValue)
            {
                var delayMin = rule.Mode == WhatsappRuleMode.OrderEvent
                    ? (rule.OrderDelay2Minutes ?? 60)
                    : (rule.DailyDelay2Minutes ?? 60);

                var planned2 = nowUtc.AddMinutes(Math.Max(1, delayMin));
                var payload2 = JsonSerializer.Serialize(new { kind = "run_now", messageNo = 2, templateId = job.Template2Id.Value });
                var id2 = await _dispatch.TryEnqueueUniqueAsync(tid, job.Id, job.RuleId, t.customerId, t.to, 2, job.Template2Id.Value, planned2, localDate, payload2, ct);
                if (id2.HasValue) enq++;
            }
        }

        return Ok(new { enqueued = enq });
    }
    internal static class TimeZones
    {
        public static readonly TimeZoneInfo Istanbul = Find("Europe/Istanbul", "Turkey Standard Time");

        private static TimeZoneInfo Find(params string[] ids)
        {
            foreach (var id in ids)
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { }
            }
            return TimeZoneInfo.Utc;
        }
    }

}
