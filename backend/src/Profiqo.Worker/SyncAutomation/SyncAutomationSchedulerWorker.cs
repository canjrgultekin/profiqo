using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Npgsql;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Integrations.Jobs;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Infrastructure.Persistence.Entities;
using Profiqo.Worker.Tenancy;

namespace Profiqo.Worker.SyncAutomation;

internal sealed class SyncAutomationSchedulerWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SyncAutomationSchedulerWorker> _logger;
    private readonly string _workerId = $"sync-scheduler-{Environment.MachineName}-{Guid.NewGuid():N}";

    public SyncAutomationSchedulerWorker(IServiceProvider sp, ILogger<SyncAutomationSchedulerWorker> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncAutomationSchedulerWorker started. workerId={WorkerId}", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ProfiqoDbContext>();
                var jobs = scope.ServiceProvider.GetRequiredService<IIntegrationJobRepository>();
                var connectionsRepo = scope.ServiceProvider.GetRequiredService<IProviderConnectionRepository>();
                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>();

                var claimed = await TryClaimDueRuleAsync(db, stoppingToken);

                if (claimed is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                var (ruleId, tenantGuid) = claimed.Value;
                tenantSetter.Set(new TenantId(tenantGuid));

                try
                {
                    var strategy = db.Database.CreateExecutionStrategy();

                    await strategy.ExecuteAsync(async () =>
                    {
                        var now = DateTimeOffset.UtcNow;

                        await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);

                        var rule = await db.Set<SyncAutomationRule>()
                            .FirstOrDefaultAsync(x => x.Id == ruleId, stoppingToken);

                        if (rule is null)
                        {
                            await tx.CommitAsync(stoppingToken);
                            return;
                        }

                        if (rule.Status != 1)
                        {
                            rule.ReleaseLock(now);
                            await db.SaveChangesAsync(stoppingToken);
                            await tx.CommitAsync(stoppingToken);
                            return;
                        }

                        var connIds = await db.Set<SyncAutomationRuleConnection>()
                            .AsNoTracking()
                            .Where(x => x.TenantId == tenantGuid && x.RuleId == ruleId)
                            .Select(x => x.ConnectionId)
                            .ToListAsync(stoppingToken);

                        if (connIds.Count == 0)
                        {
                            rule.TouchScheduled(now);
                            await db.SaveChangesAsync(stoppingToken);
                            await tx.CommitAsync(stoppingToken);
                            return;
                        }

                        var batchId = Guid.NewGuid();
                        await db.Set<SyncAutomationBatch>().AddAsync(new SyncAutomationBatch(batchId, tenantGuid, ruleId, now), stoppingToken);

                        foreach (var cid in connIds)
                        {
                            var conn = await connectionsRepo.GetByIdAsync(new ProviderConnectionId(cid), stoppingToken);
                            if (conn is null || conn.TenantId.Value != tenantGuid || conn.Status != ProviderConnectionStatus.Active)
                                continue;

                            // Provider bazlı job seti
                            if (conn.ProviderType == ProviderType.Ikas)
                            {
                                await jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantGuid, cid, IntegrationJobKind.IkasSyncCustomers, rule.PageSize, rule.MaxPages), stoppingToken);
                                await jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantGuid, cid, IntegrationJobKind.IkasSyncOrders, rule.PageSize, rule.MaxPages), stoppingToken);
                                await jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantGuid, cid, IntegrationJobKind.IkasSyncAbandonedCheckouts, rule.PageSize, rule.MaxPages), stoppingToken);
                                await jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantGuid, cid, IntegrationJobKind.IkasSyncProducts, rule.PageSize, rule.MaxPages), stoppingToken);
                            }
                            else if (conn.ProviderType == ProviderType.Trendyol)
                            {
                                await jobs.CreateAsync(new IntegrationJobCreateRequest(batchId, tenantGuid, cid, IntegrationJobKind.TrendyolSyncOrders, rule.PageSize, rule.MaxPages), stoppingToken);
                            }
                        }

                        rule.TouchScheduled(now);

                        await db.SaveChangesAsync(stoppingToken);
                        await tx.CommitAsync(stoppingToken);
                    });
                }
                finally
                {
                    tenantSetter.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler loop error.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task<(Guid ruleId, Guid tenantId)?> TryClaimDueRuleAsync(ProfiqoDbContext db, CancellationToken ct)
    {
        // lock timeout: 10 dakika
        var sql = @"
WITH cte AS (
  SELECT id
  FROM sync_automation_rules
  WHERE status = @active
    AND next_run_at_utc <= now()
    AND (locked_at_utc IS NULL OR locked_at_utc < now() - interval '10 minutes')
  ORDER BY next_run_at_utc
  LIMIT 1
  FOR UPDATE SKIP LOCKED
)
UPDATE sync_automation_rules r
SET locked_by = @workerId,
    locked_at_utc = now(),
    updated_at_utc = now()
FROM cte
WHERE r.id = cte.id
RETURNING r.id, r.tenant_id;
";

        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new NpgsqlParameter("active", (short)1));
            cmd.Parameters.Add(new NpgsqlParameter("workerId", _workerId));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            var ruleId = reader.GetGuid(0);
            var tenantId = reader.GetGuid(1);

            return (ruleId, tenantId);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
