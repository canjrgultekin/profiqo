using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Integrations;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/automation/sync")]
[Authorize(Policy = AuthorizationPolicies.ReportAccess)]
public sealed class SyncAutomationController : ControllerBase
{
    private static readonly int[] AllowedIntervals = [180, 360, 720, 1440, 10080];

    // ✅ NEW: allowed job kinds
    private static readonly HashSet<string> AllowedJobKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ikas.customers",
        "ikas.orders",
        "ikas.abandoned",
        "trendyol.orders"
    };

    private readonly ITenantContext _tenant;
    private readonly ProfiqoDbContext _db;

    public SyncAutomationController(ITenantContext tenant, ProfiqoDbContext db)
    {
        _tenant = tenant;
        _db = db;
    }

    [HttpGet("connections")]
    public async Task<IActionResult> ListConnections(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Missing tenant context" });

        var items = await _db.ProviderConnections.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value)
            .OrderBy(x => x.ProviderType)
            .Select(x => new
            {
                connectionId = x.Id.Value,
                providerType = x.ProviderType.ToString().ToLowerInvariant(),
                displayName = x.DisplayName,
                status = x.Status.ToString().ToLowerInvariant()
            })
            .ToListAsync(ct);

        return Ok(new { items });
    }

    public sealed record CreateRuleRequest(
        string Name,
        int IntervalMinutes,
        IReadOnlyList<Guid> ConnectionIds,
        int? PageSize,
        int? MaxPages,
        IReadOnlyList<string>? JobKinds,   // ✅ NEW
        int? JitterMinutes                // ✅ NEW (0..10)
    );

    [HttpGet("rules")]
    public async Task<IActionResult> ListRules(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Missing tenant context" });

        var tenantGuid = tenantId.Value.Value;

        var rules = await _db.Set<SyncAutomationRule>().AsNoTracking()
            .Where(x => x.TenantId == tenantGuid)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        var ruleIds = rules.Select(r => r.Id).ToList();

        var rel = await _db.Set<SyncAutomationRuleConnection>().AsNoTracking()
            .Where(x => x.TenantId == tenantGuid && ruleIds.Contains(x.RuleId))
            .ToListAsync(ct);

        var map = rel.GroupBy(x => x.RuleId).ToDictionary(g => g.Key, g => g.Select(x => x.ConnectionId).ToArray());

        object[] ParseJobKinds(string json)
        {
            try
            {
                var arr = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
                return arr.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => (object)x.Trim()).Distinct().ToArray();
            }
            catch
            {
                return Array.Empty<object>();
            }
        }

        var items = rules.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            status = r.Status == 1 ? "active" : "paused",
            intervalMinutes = r.IntervalMinutes,
            pageSize = r.PageSize,
            maxPages = r.MaxPages,
            jitterMinutes = r.JitterMinutes,             // ✅ NEW
            jobKinds = ParseJobKinds(r.JobKindsJson),    // ✅ NEW
            nextRunAtUtc = r.NextRunAtUtc,
            lastEnqueuedAtUtc = r.LastEnqueuedAtUtc,
            connectionIds = map.TryGetValue(r.Id, out var ids) ? ids : Array.Empty<Guid>()
        });

        return Ok(new { items });
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest req, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Missing tenant context" });

        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Name required" });
        if (!AllowedIntervals.Contains(req.IntervalMinutes)) return BadRequest(new { message = "Invalid intervalMinutes" });
        if (req.ConnectionIds is null || req.ConnectionIds.Count == 0) return BadRequest(new { message = "At least one connection required" });

        var jitter = req.JitterMinutes is null ? 0 : Math.Clamp(req.JitterMinutes.Value, 0, 10);

        var jobKinds = (req.JobKinds ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        // ✅ Backwards compatible: if client sends nothing, allow empty, scheduler will fallback to default set.
        // Ama UI'da mutlaka seçtireceğiz.
        foreach (var k in jobKinds)
        {
            if (!AllowedJobKinds.Contains(k))
                return BadRequest(new { message = $"Invalid jobKind: {k}" });
        }

        var jobKindsJson = JsonSerializer.Serialize(jobKinds);

        var now = DateTimeOffset.UtcNow;
        var ruleId = Guid.NewGuid();
        var tenantGuid = tenantId.Value.Value;

        var pageSize = req.PageSize is >= 10 and <= 500 ? req.PageSize.Value : 100;
        var maxPages = req.MaxPages is >= 1 and <= 500 ? req.MaxPages.Value : 50;

        var rule = new SyncAutomationRule(
            id: ruleId,
            tenantId: tenantGuid,
            name: req.Name.Trim(),
            status: 1,
            intervalMinutes: req.IntervalMinutes,
            pageSize: pageSize,
            maxPages: maxPages,
            nowUtc: now,
            jitterMinutes: jitter,
            jobKindsJson: jobKindsJson);

        await _db.Set<SyncAutomationRule>().AddAsync(rule, ct);

        foreach (var cid in req.ConnectionIds.Distinct())
        {
            await _db.Set<SyncAutomationRuleConnection>()
                .AddAsync(new SyncAutomationRuleConnection(tenantGuid, ruleId, cid, now), ct);
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { id = ruleId });
    }

    [HttpPost("rules/{id:guid}/pause")]
    public async Task<IActionResult> PauseRule(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Missing tenant context" });

        var tenantGuid = tenantId.Value.Value;

        var rule = await _db.Set<SyncAutomationRule>()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantGuid, ct);

        if (rule is null) return NotFound();

        rule.Pause(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);

        return Ok();
    }

    [HttpPost("rules/{id:guid}/activate")]
    public async Task<IActionResult> ActivateRule(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Missing tenant context" });

        var tenantGuid = tenantId.Value.Value;

        var rule = await _db.Set<SyncAutomationRule>()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantGuid, ct);

        if (rule is null) return NotFound();

        rule.Activate(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);

        return Ok();
    }
}
