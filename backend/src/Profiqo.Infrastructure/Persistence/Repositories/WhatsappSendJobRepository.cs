using Microsoft.EntityFrameworkCore;

using Npgsql;

using Profiqo.Application.Abstractions.Persistence.Whatsapp;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class WhatsappSendJobRepository : IWhatsappSendJobRepository
{
    private readonly ProfiqoDbContext _db;

    public WhatsappSendJobRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateAsync(WhatsappSendJobCreateRequest req, CancellationToken ct)
    {
        var id = Guid.NewGuid();

        var entity = new WhatsappSendJob(
            id: id,
            tenantId: req.TenantId,
            connectionId: req.ConnectionId,
            payloadJson: req.PayloadJson,
            nextAttemptAtUtc: req.NextAttemptAtUtc);

        await _db.Set<WhatsappSendJob>().AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return id;
    }

    public async Task<WhatsappSendJobDto?> GetAsync(Guid jobId, CancellationToken ct)
    {
        var j = await _db.Set<WhatsappSendJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId, ct);

        return j is null ? null : ToDto(j);
    }

    public async Task<WhatsappSendJobClaimDto?> TryClaimNextAsync(string workerId, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var sql = @"
WITH cte AS (
    SELECT id
    FROM whatsapp_send_jobs
    WHERE (status = @queued OR status = @retrying)
      AND next_attempt_at_utc <= @nowUtc
    ORDER BY next_attempt_at_utc, created_at_utc
    LIMIT 1
    FOR UPDATE SKIP LOCKED
)
UPDATE whatsapp_send_jobs j
SET status = @running,
    locked_by = @workerId,
    locked_at_utc = now(),
    started_at_utc = COALESCE(started_at_utc, now()),
    updated_at_utc = now()
FROM cte
WHERE j.id = cte.id
RETURNING j.id";

        var connection = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(ct);

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            cmd.Parameters.Add(new NpgsqlParameter("queued", (short)WhatsappSendJobStatus.Queued));
            cmd.Parameters.Add(new NpgsqlParameter("retrying", (short)WhatsappSendJobStatus.Retrying));
            cmd.Parameters.Add(new NpgsqlParameter("running", (short)WhatsappSendJobStatus.Running));
            cmd.Parameters.Add(new NpgsqlParameter("workerId", workerId));
            cmd.Parameters.Add(new NpgsqlParameter("nowUtc", nowUtc.ToUniversalTime()));

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is null || result == DBNull.Value)
                return null;

            var jobId = (Guid)result;

            var job = await _db.Set<WhatsappSendJob>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == jobId, ct);

            if (job is null) return null;

            return new WhatsappSendJobClaimDto(
                Id: job.Id,
                TenantId: job.TenantId,
                ConnectionId: job.ConnectionId,
                Status: job.Status,
                AttemptCount: job.AttemptCount,
                NextAttemptAtUtc: job.NextAttemptAtUtc,
                PayloadJson: job.PayloadJson);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task MarkSucceededAsync(Guid jobId, CancellationToken ct)
    {
        var j = await _db.Set<WhatsappSendJob>().FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (j is null) return;

        j.MarkSucceeded();
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct)
    {
        var j = await _db.Set<WhatsappSendJob>().FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (j is null) return;

        j.MarkFailed(error);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkRetryingAsync(Guid jobId, DateTimeOffset nextAttemptAtUtc, string error, CancellationToken ct)
    {
        var j = await _db.Set<WhatsappSendJob>().FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (j is null) return;

        j.MarkRetrying(nextAttemptAtUtc, error);
        await _db.SaveChangesAsync(ct);
    }


    public async Task<int> ReleaseStaleLocksAsync(TimeSpan lockTtl, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - lockTtl;

        var sql = @"
UPDATE whatsapp_send_jobs
SET status = @retrying,
    locked_by = NULL,
    locked_at_utc = NULL,
    updated_at_utc = now(),
    next_attempt_at_utc = now()
WHERE status = @running
  AND locked_at_utc IS NOT NULL
  AND locked_at_utc < @cutoff;
";

        var p1 = new NpgsqlParameter("retrying", (short)WhatsappSendJobStatus.Retrying);
        var p2 = new NpgsqlParameter("running", (short)WhatsappSendJobStatus.Running);
        var p3 = new NpgsqlParameter("cutoff", cutoff);

        return await _db.Database.ExecuteSqlRawAsync(sql, new[] { p1, p2, p3 }, ct);
    }

private static WhatsappSendJobDto ToDto(WhatsappSendJob j)
        => new(
            Id: j.Id,
            TenantId: j.TenantId,
            ConnectionId: j.ConnectionId,
            Status: j.Status,
            AttemptCount: j.AttemptCount,
            NextAttemptAtUtc: j.NextAttemptAtUtc,
            CreatedAtUtc: j.CreatedAtUtc,
            UpdatedAtUtc: j.UpdatedAtUtc,
            StartedAtUtc: j.StartedAtUtc,
            FinishedAtUtc: j.FinishedAtUtc,
            LastError: j.LastError);
}
