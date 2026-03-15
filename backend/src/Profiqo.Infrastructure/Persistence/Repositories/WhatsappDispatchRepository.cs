using Microsoft.EntityFrameworkCore;

using Npgsql;

using Profiqo.Application.Abstractions.Persistence.WhatsappAutomation;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class WhatsappDispatchRepository : IWhatsappDispatchRepository
{
    private readonly ProfiqoDbContext _db;

    public WhatsappDispatchRepository(ProfiqoDbContext db) => _db = db;

    public async Task<IReadOnlyList<WhatsappDispatchDto>> ListRecentAsync(Guid tenantId, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 500);

        var rows = await _db.Set<WhatsappDispatchQueueRow>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Guid> EnqueueManualAsync(
        Guid tenantId, Guid jobId, Guid ruleId, Guid customerId, string toE164,
        short messageNo, Guid templateId, DateTimeOffset plannedAtUtc, DateOnly localDate,
        string payloadJson, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var row = new WhatsappDispatchQueueRow
        {
            Id = id,
            TenantId = tenantId,
            JobId = jobId,
            RuleId = ruleId,
            CustomerId = customerId,
            ToE164 = toE164,
            MessageNo = messageNo,
            TemplateId = templateId,
            PlannedAtUtc = plannedAtUtc,
            LocalDate = localDate,
            Status = WhatsappDispatchStatus.Queued,
            AttemptCount = 0,
            NextAttemptAtUtc = plannedAtUtc,
            PayloadJson = payloadJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _db.AddAsync(row, ct);
        await _db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<Guid?> TryEnqueueUniqueAsync(
        Guid tenantId, Guid jobId, Guid ruleId, Guid customerId, string toE164,
        short messageNo, Guid templateId, DateTimeOffset plannedAtUtc, DateOnly localDate,
        string payloadJson, CancellationToken ct)
    {
        try
        {
            var id = await EnqueueManualAsync(tenantId, jobId, ruleId, customerId, toE164, messageNo, templateId, plannedAtUtc, localDate, payloadJson, ct);
            return id;
        }
        catch (DbUpdateException)
        {
            return null;
        }
    }

    public async Task<(Guid Id, Guid TenantId, Guid JobId, Guid RuleId, Guid CustomerId, string ToE164, short MessageNo, Guid TemplateId, DateTimeOffset PlannedAtUtc, DateOnly LocalDate, string PayloadJson, int AttemptCount)?>
        TryClaimNextAsync(string workerId, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var sql = @"
WITH cte AS (
    SELECT id
    FROM whatsapp_dispatch_queue
    WHERE (status = @queued OR status = @failed)
      AND next_attempt_at_utc <= @nowUtc
    ORDER BY next_attempt_at_utc, planned_at_utc
    LIMIT 1
    FOR UPDATE SKIP LOCKED
)
UPDATE whatsapp_dispatch_queue q
SET status = @running,
    locked_by = @workerId,
    locked_at_utc = now(),
    updated_at_utc = now()
FROM cte
WHERE q.id = cte.id
RETURNING q.id;
";

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;

            cmd.Parameters.Add(new NpgsqlParameter("queued", (short)WhatsappDispatchStatus.Queued));
            cmd.Parameters.Add(new NpgsqlParameter("failed", (short)WhatsappDispatchStatus.Failed));
            cmd.Parameters.Add(new NpgsqlParameter("running", (short)WhatsappDispatchStatus.Running));
            cmd.Parameters.Add(new NpgsqlParameter("workerId", workerId));
            cmd.Parameters.Add(new NpgsqlParameter("nowUtc", nowUtc));

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is null || result == DBNull.Value) return null;

            var id = (Guid)result;

            var row = await _db.Set<WhatsappDispatchQueueRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (row is null) return null;

            return (row.Id, row.TenantId, row.JobId, row.RuleId, row.CustomerId, row.ToE164, row.MessageNo, row.TemplateId, row.PlannedAtUtc, row.LocalDate, row.PayloadJson, row.AttemptCount);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
    public Task MarkSentDummyAsync(Guid id, CancellationToken ct)
        => MarkSentAsync(id, true, ct);

    public async Task MarkSentAsync(Guid id, bool isSimulated, CancellationToken ct)
    {
        const string sql = @"
UPDATE whatsapp_dispatch_queue
SET status = @sent,
    is_simulated = @is_simulated,
    sent_at_utc = now(),
    locked_by = NULL,
    locked_at_utc = NULL,
    last_error = NULL,
    updated_at_utc = now()
WHERE id = @id;
";
        var p1 = new Npgsql.NpgsqlParameter("sent", (short)WhatsappDispatchStatus.SentDummy); // status=3, is_simulated belirleyecek
        var p2 = new Npgsql.NpgsqlParameter("is_simulated", isSimulated);
        var p3 = new Npgsql.NpgsqlParameter("id", id);

        await _db.Database.ExecuteSqlRawAsync(sql, new[] { p1, p2, p3 }, ct);
    }

    public async Task MarkSuppressedLimitAsync(Guid id, string reason, CancellationToken ct)
    {
        var row = await _db.Set<WhatsappDispatchQueueRow>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;

        row.Status = WhatsappDispatchStatus.SuppressedLimit;
        row.SentAtUtc = DateTimeOffset.UtcNow;
        row.LastError = reason.Length > 7800 ? reason[..7800] : reason;
        row.LockedBy = null;
        row.LockedAtUtc = null;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string error, DateTimeOffset nextAttemptAtUtc, CancellationToken ct)
    {
        var row = await _db.Set<WhatsappDispatchQueueRow>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;

        row.Status = WhatsappDispatchStatus.Failed;
        row.AttemptCount = Math.Min(row.AttemptCount + 1, 1_000_000);
        row.NextAttemptAtUtc = nextAttemptAtUtc;
        row.LastError = error.Length > 7800 ? error[..7800] : error;
        row.LockedBy = null;
        row.LockedAtUtc = null;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryConsumeDailyQuotaAsync(Guid tenantId, Guid customerId, DateOnly localDate, short limit, CancellationToken ct)
    {
        var sql = @"
INSERT INTO whatsapp_customer_daily_quota(tenant_id, customer_id, local_date, used_count, updated_at_utc)
VALUES (@tenant, @customer, @date, 1, now())
ON CONFLICT (tenant_id, customer_id, local_date)
DO UPDATE SET
  used_count = whatsapp_customer_daily_quota.used_count + 1,
  updated_at_utc = now()
WHERE whatsapp_customer_daily_quota.used_count < @limit
RETURNING used_count;
";

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;

            cmd.Parameters.Add(new NpgsqlParameter("tenant", tenantId));
            cmd.Parameters.Add(new NpgsqlParameter("customer", customerId));
            cmd.Parameters.Add(new NpgsqlParameter("date", localDate));
            cmd.Parameters.Add(new NpgsqlParameter("limit", limit));

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is not null && result != DBNull.Value;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task<int> ReleaseStaleLocksAsync(TimeSpan lockTtl, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - lockTtl;

        var sql = @"
UPDATE whatsapp_dispatch_queue
SET status = @queued,
    locked_by = NULL,
    locked_at_utc = NULL,
    updated_at_utc = now()
WHERE status = @running
  AND locked_at_utc IS NOT NULL
  AND locked_at_utc < @cutoff;
";

        var p1 = new NpgsqlParameter("queued", (short)WhatsappDispatchStatus.Queued);
        var p2 = new NpgsqlParameter("running", (short)WhatsappDispatchStatus.Running);
        var p3 = new NpgsqlParameter("cutoff", cutoff);

        return await _db.Database.ExecuteSqlRawAsync(sql, new[] { p1, p2, p3 }, ct);
    }

    private static WhatsappDispatchDto ToDto(WhatsappDispatchQueueRow r)
        => new(r.Id, r.TenantId, r.JobId, r.RuleId, r.CustomerId, r.ToE164, r.MessageNo, r.TemplateId, r.PlannedAtUtc, r.LocalDate, r.Status, r.AttemptCount, r.NextAttemptAtUtc, r.SentAtUtc, r.LastError, r.CreatedAtUtc, r.UpdatedAtUtc);
}
