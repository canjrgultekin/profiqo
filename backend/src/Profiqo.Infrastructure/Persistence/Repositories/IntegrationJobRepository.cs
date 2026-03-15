using Microsoft.EntityFrameworkCore;

using Npgsql;

using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Integrations.Jobs;
using Profiqo.Infrastructure.Persistence.Entities;

namespace Profiqo.Infrastructure.Persistence.Repositories;

internal sealed class IntegrationJobRepository : IIntegrationJobRepository
{
    private readonly ProfiqoDbContext _db;

    public IntegrationJobRepository(ProfiqoDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateAsync(IntegrationJobCreateRequest req, CancellationToken ct)
    {
        var id = Guid.NewGuid();

        var entity = new IntegrationJob(
            id: id,
            batchId: req.BatchId,
            tenantId: req.TenantId,
            connectionId: req.ConnectionId,
            kind: req.Kind,
            pageSize: req.PageSize,
            maxPages: req.MaxPages);

        await _db.Set<IntegrationJob>().AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return id;
    }

    public async Task<IntegrationJobDto?> GetAsync(Guid jobId, CancellationToken ct)
    {
        var j = await _db.Set<IntegrationJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId, ct);

        return j is null ? null : ToDto(j);
    }

    public async Task<IReadOnlyList<IntegrationJobDto>> ListByBatchAsync(Guid batchId, CancellationToken ct)
    {
        var list = await _db.Set<IntegrationJob>()
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<IntegrationJobDto?> TryClaimNextAsync(string workerId, CancellationToken ct)
    {
        // PostgreSQL data-modifying CTE must be top-level, can't use FromSqlRaw
        // Solution: Use raw ADO.NET command to execute CTE and return the claimed job id

        var sql = @"
WITH cte AS (
    SELECT id
    FROM integration_jobs
    WHERE status = @queued
    ORDER BY created_at_utc
    LIMIT 1
    FOR UPDATE SKIP LOCKED
)
UPDATE integration_jobs j
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
            cmd.Parameters.Add(new NpgsqlParameter("queued", (short)IntegrationJobStatus.Queued));
            cmd.Parameters.Add(new NpgsqlParameter("running", (short)IntegrationJobStatus.Running));
            cmd.Parameters.Add(new NpgsqlParameter("workerId", workerId));

            var result = await cmd.ExecuteScalarAsync(ct);

            if (result is null || result == DBNull.Value)
                return null;

            var jobId = (Guid)result;

            // Fetch the full entity
            var job = await _db.Set<IntegrationJob>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == jobId, ct);

            return job is null ? null : ToDto(job);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task MarkProgressAsync(Guid jobId, int processedItems, CancellationToken ct)
    {
        var j = await _db.Set<IntegrationJob>().FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (j is null) return;

        j.MarkProgress(processedItems);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkSucceededAsync(Guid jobId, CancellationToken ct)
    {
        var j = await _db.Set<IntegrationJob>().FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (j is null) return;

        j.MarkSucceeded();
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct)
    {
        var j = await _db.Set<IntegrationJob>().FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (j is null) return;

        j.MarkFailed(error);
        await _db.SaveChangesAsync(ct);
    }

    private static IntegrationJobDto ToDto(IntegrationJob j)
        => new(
            JobId: j.Id,
            BatchId: j.BatchId,
            TenantId: j.TenantId,
            ConnectionId: j.ConnectionId,
            Kind: j.Kind,
            Status: j.Status,
            PageSize: j.PageSize,
            MaxPages: j.MaxPages,
            ProcessedItems: j.ProcessedItems,
            CreatedAtUtc: j.CreatedAtUtc,
            StartedAtUtc: j.StartedAtUtc,
            FinishedAtUtc: j.FinishedAtUtc,
            LastError: j.LastError);
}