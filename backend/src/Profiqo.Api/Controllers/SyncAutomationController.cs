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
        int? MaxPages);

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

        var items = rules.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            status = r.Status == 1 ? "active" : "paused",
            intervalMinutes = r.IntervalMinutes,
            pageSize = r.PageSize,
            maxPages = r.MaxPages,
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
            nowUtc: now);

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

    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Missing tenant context" });

        var tenantGuid = tenantId.Value.Value;
        if (take <= 0) take = 50;
        if (take > 200) take = 200;

        var batches = await _db.Set<SyncAutomationBatch>().AsNoTracking()
            .Where(x => x.TenantId == tenantGuid)
            .OrderByDescending(x => x.ScheduledAtUtc)
            .Take(take)
            .ToListAsync(ct);

        var batchIds = batches.Select(x => x.BatchId).ToList();

        var jobsAgg = await _db.Set<IntegrationJob>().AsNoTracking()
            .Where(j => batchIds.Contains(j.BatchId))
            .GroupBy(j => j.BatchId)
            .Select(g => new
            {
                batchId = g.Key,
                total = g.Count(),
                queued = g.Count(x => x.Status == Application.Integrations.Jobs.IntegrationJobStatus.Queued),
                running = g.Count(x => x.Status == Application.Integrations.Jobs.IntegrationJobStatus.Running),
                succeeded = g.Count(x => x.Status == Application.Integrations.Jobs.IntegrationJobStatus.Succeeded),
                failed = g.Count(x => x.Status == Application.Integrations.Jobs.IntegrationJobStatus.Failed),
                lastError = g.Where(x => x.LastError != null).OrderByDescending(x => x.UpdatedAtUtc).Select(x => x.LastError).FirstOrDefault()
            })
            .ToListAsync(ct);

        var aggMap = jobsAgg.ToDictionary(x => x.batchId, x => x);

        string StatusOf(dynamic a)
        {
            if (a.failed > 0) return "failed";
            if (a.total > 0 && a.succeeded == a.total) return "succeeded";
            if (a.running > 0) return "running";
            return "queued";
        }

        var items = batches.Select(b =>
        {
            if (!aggMap.TryGetValue(b.BatchId, out var a))
            {
                return new
                {
                    batchId = b.BatchId,
                    ruleId = b.RuleId,
                    scheduledAtUtc = b.ScheduledAtUtc,
                    status = "queued",
                    totalJobs = 0,
                    lastError = (string?)null
                };
            }

            return new
            {
                batchId = b.BatchId,
                ruleId = b.RuleId,
                scheduledAtUtc = b.ScheduledAtUtc,
                status = StatusOf(a),
                totalJobs = (int)a.total,
                lastError = (string?)a.lastError
            };
        });

        return Ok(new { items });
    }

    [HttpGet("runs/{batchId:guid}")]
    public async Task<IActionResult> GetRun(Guid batchId, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return BadRequest(new { message = "Missing tenant context" });

        var tenantGuid = tenantId.Value.Value;

        var batch = await _db.Set<SyncAutomationBatch>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.BatchId == batchId && x.TenantId == tenantGuid, ct);

        if (batch is null) return NotFound();

        var jobs = await _db.Set<IntegrationJob>().AsNoTracking()
            .Where(j => j.BatchId == batchId)
            .OrderBy(j => j.CreatedAtUtc)
            .Select(j => new
            {
                jobId = j.Id,
                kind = j.Kind.ToString(),
                status = j.Status.ToString(),
                connectionId = j.ConnectionId,
                processedItems = j.ProcessedItems,
                createdAtUtc = j.CreatedAtUtc,
                startedAtUtc = j.StartedAtUtc,
                finishedAtUtc = j.FinishedAtUtc,
                lastError = j.LastError
            })
            .ToListAsync(ct);

        return Ok(new
        {
            batchId = batch.BatchId,
            ruleId = batch.RuleId,
            scheduledAtUtc = batch.ScheduledAtUtc,
            jobs
        });
    }
}
